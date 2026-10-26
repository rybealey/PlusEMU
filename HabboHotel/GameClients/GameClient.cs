using System.Net.Sockets;
using NLog;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.Communication.Revisions;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.GameClients;

public abstract class GameClient
{
    private readonly IGameServer _server;
    private readonly IPacketFactory _packetFactory;
    private static readonly ILogger Log = LogManager.GetLogger("Plus.HabboHotel.GameClients.GameClient");
    private Habbo? _habbo;

    // pixelrp: set once the connection has closed. Guarded by a lock that
    // SetHabbo takes as well - see OnDisconnected.
    private readonly object _disconnectLock = new();
    private bool _disconnected;

    /// <summary>
    /// pixelrp: true once the connection has closed. A login is not stopped
    /// by its connection dropping - its awaits just carry on - so the login
    /// path asks this (Authenticator, SsoTicketEvent).
    /// </summary>
    public bool IsDisconnected
    {
        get
        {
            lock (_disconnectLock)
                return _disconnected;
        }
    }

    public bool IsAuthenticated { get; set; } = false;
    public DateTime TimeConnected { get; set; }

    [Obsolete("Will be removed")]
    public string MachineId { get; set; } = string.Empty;

    [Obsolete("Will be removed")]
    public int PingCount { get; set; }

    public Revision Revision { get; set; }

    internal Func<SocketAsyncEventArgs, bool> SendCallback { get; set; }
    internal Action? DisconnectRequested { get; set; }

    public Guid Id { get; set; }


    // pixelrp: set the moment a disconnect is asked for, so the receive loop stops
    // handling this connection's packets straight away, without waiting for the socket
    // to report the close.
    private volatile bool _disconnectRequested;

    public void Disconnect()
    {
        _disconnectRequested = true;
        DisconnectRequested?.Invoke();
    }

    protected GameClient(IGameServer server, IPacketFactory packetFactory)
    {
        _packetFactory = packetFactory;
        _server = server;
    }

    internal void OnDisconnected()
    {
        // pixelrp: the flag is set and the Habbo read in one step, under the
        // lock SetHabbo takes too. A login whose connection drops therefore
        // either has its Habbo seen here and logged out, or finds the flag set
        // itself straight after SetHabbo (Authenticator) - never neither.
        Habbo? habbo;
        lock (_disconnectLock)
        {
            _disconnected = true;
            habbo = _habbo;
        }
        // pixelrp: NetCoreServer only unregisters the session after this
        // returns, so a throw out of the logout left a dead session in its
        // lists for good. The logout guards its own steps (Habbo.OnDisconnect);
        // this is the last line behind them.
        try
        {
            habbo?.OnDisconnect();
        }
        catch (Exception e)
        {
            Log.Error(e, $"Logout failed for session {Id}");
        }
    }

    /// <summary>
    /// Upper bound on a single incoming frame. Real client packets are a few hundred bytes at
    /// most (a phone photo is the big exception); this only exists to bound what a hostile
    /// client can make us buffer.
    /// </summary>
    internal const int MaxPacketLength = 1024 * 1024;

    /// <summary>
    /// pixelrp: the most that may wait here unhandled - one packet at the limit above plus
    /// room for what arrived behind it. Past this the connection is dropped. WsSessionProxy
    /// holds a WebSocket message to the same limit while the socket library assembles it.
    /// </summary>
    internal const int MaxBufferedBytes = MaxPacketLength + 64 * 1024;

    // pixelrp: what has arrived and not been handled yet - at most one unfinished packet
    // plus whatever came in behind it. New bytes go on the end; packets are read off the
    // front from _receivedStart. Guarded by _receiveLock, as is _handling: true while a
    // HandleReceivedAsync loop is working through this connection's packets.
    private readonly object _receiveLock = new();
    private byte[] _received = Array.Empty<byte>();
    private int _receivedStart;
    private int _receivedEnd;
    private bool _handling;

    // A connection that once sent a photo would otherwise keep a megabyte buffer for good.
    private const int KeptBufferBytes = 16 * 1024;

    internal abstract (bool Complete, uint MessageId, int HeaderLength, int Length) GetMessageIdAndPacketLength(ReadOnlyMemory<byte> buffer);

    /// <summary>
    /// pixelrp: bytes in from the socket, on the socket library's receive thread. Nothing
    /// thrown in here may escape that thread: anything unexpected drops this one connection.
    ///
    /// The bytes are copied before returning (the socket library reuses its buffer) and
    /// handled by one loop per connection, so one player's packets run one at a time and in
    /// the order they were sent.
    /// </summary>
    internal virtual void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            if (size <= 0 || _disconnectRequested || IsDisconnected)
                return;
            var tooMuch = false;
            var start = false;
            lock (_receiveLock)
            {
                if (size > MaxBufferedBytes - (_receivedEnd - _receivedStart))
                {
                    tooMuch = true;
                }
                else
                {
                    Append(buffer, (int)offset, (int)size);
                    start = !_handling;
                    _handling = true;
                }
            }
            if (tooMuch)
            {
                Log.Warn($"Session {Id} sent more than {MaxBufferedBytes} bytes without completing a packet - disconnecting.");
                Disconnect();
                return;
            }
            if (start)
                _ = HandleReceivedAsync();
        }
        catch (Exception e)
        {
            Log.Error(e, $"Receive failed for session {Id} - disconnecting.");
            Disconnect();
        }
    }

    // Under _receiveLock. Copies the new bytes onto the end, first moving what is still
    // unread to the front, then growing the buffer if that is not enough room.
    private void Append(byte[] buffer, int offset, int size)
    {
        var unread = _receivedEnd - _receivedStart;
        if (_receivedEnd + size > _received.Length)
        {
            var needed = unread + size;
            var target = needed <= _received.Length
                ? _received
                : new byte[Math.Min(MaxBufferedBytes, Math.Max(needed, Math.Max(4096, _received.Length * 2)))];
            if (unread > 0)
                System.Buffer.BlockCopy(_received, _receivedStart, target, 0, unread);
            _received = target;
            _receivedStart = 0;
            _receivedEnd = unread;
        }
        System.Buffer.BlockCopy(buffer, offset, _received, _receivedEnd, size);
        _receivedEnd += size;
    }

    // One per connection at a time (_handling): takes whole packets off the front of the
    // buffer and hands each to the packet manager, waiting for it to finish before the next.
    // Stops when only an unfinished packet is left - the next bytes in start it again - or
    // when the connection has closed.
    private async Task HandleReceivedAsync()
    {
        try
        {
            while (true)
            {
                uint messageId = 0;
                byte[]? body = null;
                var malformed = false;
                var length = 0;
                var closed = _disconnectRequested || IsDisconnected;
                lock (_receiveLock)
                {
                    if (closed)
                    {
                        _receivedStart = _receivedEnd = 0;
                        _received = Array.Empty<byte>();
                        _handling = false;
                        return;
                    }
                    var unread = new ReadOnlyMemory<byte>(_received, _receivedStart, _receivedEnd - _receivedStart);
                    var (complete, id, headerLength, packetLength) = GetMessageIdAndPacketLength(unread);
                    length = packetLength;
                    messageId = id;
                    if (!complete)
                    {
                        if (unread.Length == 0)
                        {
                            _receivedStart = _receivedEnd = 0;
                            if (_received.Length > KeptBufferBytes)
                                _received = Array.Empty<byte>();
                        }
                        _handling = false;
                        return;
                    }
                    // The frame length comes straight off the wire, so an injection tool can
                    // send any value it likes. There is no way to resynchronise on a bad length
                    // either (we cannot find where the next frame starts), so drop the
                    // connection.
                    if (packetLength < 0 || headerLength < 0 || packetLength > MaxPacketLength || headerLength + packetLength > unread.Length)
                    {
                        malformed = true;
                        _receivedStart = _receivedEnd = 0;
                        _received = Array.Empty<byte>();
                        _handling = false;
                    }
                    else
                    {
                        // A copy: the packet is the handler's to keep (one that outlives the
                        // packet manager's time limit still reads it), and these bytes are
                        // about to be reused.
                        body = unread.Slice(headerLength, packetLength).ToArray();
                        _receivedStart += headerLength + packetLength;
                    }
                }
                if (malformed)
                {
                    Log.Warn($"Malformed packet frame from session {Id} (message {messageId}, length {length}) - disconnecting.");
                    Disconnect();
                    return;
                }
                try
                {
                    if (Revision.IncomingIdToInternalIdMapping.TryGetValue(messageId, out var internalMessageId))
                    {
                        await _server.PacketReceived(this, internalMessageId, _packetFactory.CreateIncomingPacket(body!));
                    }
                    else
                    {
                        // TODO @80O: Add logging unknown packet received.
                    }
                }
                catch (Exception e)
                {
                    // Without this, any exception inside a packet handler is silently
                    // swallowed and the client waits forever for a reply (e.g. the
                    // navigator graying out) with nothing in the logs.
                    Log.Error(e, $"Unhandled exception while handling incoming packet {messageId}");
                }
            }
        }
        catch (Exception e)
        {
            lock (_receiveLock)
                _handling = false;
            Log.Error(e, $"Receive loop failed for session {Id} - disconnecting.");
            Disconnect();
        }
    }

    public Habbo GetHabbo() => _habbo!;

    public void SetHabbo(Habbo habbo)
    {
        // pixelrp: under the disconnect lock - see OnDisconnected.
        lock (_disconnectLock)
        {
            if (_habbo != null) throw new InvalidOperationException();
            _habbo = habbo;
        }
    }

    /// <summary>
    /// Forward this client to a room. Always use this instead of sending RoomForwardComposer
    /// directly: it records the target as a server-authorized entry, which OpenFlatConnectionEvent
    /// requires from clients below Builder (the navigator opens from Builder, so an
    /// unauthorized flat connection from them is an injected packet).
    /// </summary>
    public void SendRoomForward(uint roomId)
    {
        if (_habbo != null)
            _habbo.AuthorizedRoomEntryId = roomId;
        // pixelrp: a cold room loads while the forward makes its round trip to
        // the client and back, not after it.
        PlusEnvironment.Game.RoomManager.WarmInBackground(roomId);
        Send(new RoomForwardComposer(roomId));
    }

    public void Send(IServerPacket composer)
    {
        if (_batch != null && HoldInBatch(composer))
            return;
        // A composer whose header is 0, or one this revision doesn't map, used to throw
        // KeyNotFoundException straight out of whatever handler was sending it — losing
        // the rest of that handler's work rather than just the one packet. Name it in
        // the log and drop the packet instead.
        if (!Revision.InternalIdToOutgoingIdMapping.TryGetValue(composer.MessageId, out var outgoingMessageId))
        {
            Log.Warn($"No outgoing header mapped for {composer.GetType().Name} (EmuId: {composer.MessageId}) on revision {Revision.Name} - packet dropped.");
            return;
        }
        var stream = PlusMemoryStream.GetStream();
        stream.Position = 0;
        var packet = _packetFactory.CreateOutgoingPacket(stream);
        composer.Compose(packet);
        var args = new SocketAsyncEventArgs();
        var memory = stream.GetBuffer().AsMemory().Slice(0, (int)stream.Length);
        CreateHeader(memory, outgoingMessageId);
        args.SetBuffer(memory);
        SendCallback(args);
        // pixelrp: guarded, and a structured template rather than $"...". This
        // runs on EVERY outgoing packet: the interpolated form built the string
        // (plus a reflection call for the type name) before NLog could drop it,
        // so the cost was paid even with debug logging switched off.
        if (Log.IsDebugEnabled)
            Log.Debug("Send Packet: {Composer} (EmuId: {EmuId}, ClientId: {ClientId})", composer.GetType().Name, composer.MessageId, outgoingMessageId);
        stream.Dispose();
    }

    /// <summary>
    /// pixelrp: the exact bytes <see cref="Send"/> would put on the wire for this
    /// composer - body, then this client type's length/header - WITHOUT sending
    /// them. Null when this revision does not map the composer (Send would drop
    /// it too). Every client with the same type and Revision gets identical
    /// bytes, which is what lets Room.SendPacketsBatched build a frame once and
    /// hand the same bytes to everyone in the room.
    /// </summary>
    internal byte[]? Frame(IServerPacket composer)
    {
        if (!Revision.InternalIdToOutgoingIdMapping.TryGetValue(composer.MessageId, out var outgoingMessageId))
        {
            Log.Warn($"No outgoing header mapped for {composer.GetType().Name} (EmuId: {composer.MessageId}) on revision {Revision.Name} - packet dropped.");
            return null;
        }
        var stream = PlusMemoryStream.GetStream();
        try
        {
            stream.Position = 0;
            var packet = _packetFactory.CreateOutgoingPacket(stream);
            composer.Compose(packet);
            var memory = stream.GetBuffer().AsMemory().Slice(0, (int)stream.Length);
            CreateHeader(memory, outgoingMessageId);
            // A copy: the stream's buffer is pooled and goes back on Dispose.
            return memory.ToArray();
        }
        finally
        {
            stream.Dispose();
        }
    }

    /// <summary>
    /// pixelrp: send bytes already framed by <see cref="Frame"/> - one or several
    /// packets back to back, which the client's decoder splits by their length
    /// prefixes, in order, exactly as if they had arrived one message each.
    /// </summary>
    internal void SendFramed(byte[] framed)
    {
        if (framed == null || framed.Length == 0)
            return;
        if (_batch != null && HoldInBatch(framed))
            return;
        SendNow(framed);
    }

    private void SendNow(byte[] framed)
    {
        var args = new SocketAsyncEventArgs();
        args.SetBuffer(framed);
        SendCallback(args);
    }

    /// <summary>
    /// pixelrp: packets held while a batch is open (<see cref="BeginBatch"/>),
    /// framed and in the order they were sent, by any thread. Null when no
    /// batch is open - read without the lock first, so a plain send pays for
    /// nothing more than that read.
    /// </summary>
    private volatile List<byte[]>? _batch;
    private int _batchDepth;
    private readonly object _batchLock = new();

    /// <summary>
    /// pixelrp: hold everything sent to this client until the matching
    /// <see cref="EndBatch"/>, which sends it as ONE message. For a room
    /// entry: RoomReady, the room's shape, its furni and everyone in it used
    /// to go as a hundred-odd messages, each its own frame for the browser to
    /// read and hand over separately. The client's decoder splits a message by
    /// each packet's length prefix, in order, so it is the same packets in the
    /// same order. Nests: only the outermost EndBatch sends.
    /// </summary>
    public void BeginBatch()
    {
        lock (_batchLock)
        {
            _batchDepth++;
            _batch ??= new List<byte[]>();
        }
    }

    public void EndBatch()
    {
        lock (_batchLock)
        {
            if (_batchDepth == 0 || --_batchDepth > 0)
                return;
            SendHeld();
            // Cleared after the send and inside the lock: a packet sent
            // meanwhile waits for the lock, finds no batch, and goes out behind
            // the batch - never ahead of it.
            _batch = null;
        }
    }

    /// <summary>
    /// pixelrp: send what an open batch holds so far as one message, and keep
    /// the batch open for the rest - so the start of a room entry (its shape)
    /// does not wait on the database lookups made for the part behind it.
    /// Nothing happens when no batch is open.
    /// </summary>
    public void FlushBatch()
    {
        lock (_batchLock)
        {
            if (_batch == null)
                return;
            SendHeld();
            _batch.Clear();
        }
    }

    private void SendHeld()
    {
        var held = _batch;
        if (held == null || held.Count == 0)
            return;
        var framed = new byte[held.Sum(bytes => bytes.Length)];
        var offset = 0;
        foreach (var bytes in held)
        {
            Buffer.BlockCopy(bytes, 0, framed, offset, bytes.Length);
            offset += bytes.Length;
        }
        SendNow(framed);
    }

    private bool HoldInBatch(IServerPacket composer)
    {
        // Composed before taking the lock, never under it: a composer that
        // takes a lock of its own (a room's, say) while another thread holds
        // that lock and is sending to this client would deadlock the two. Null
        // when this revision does not map the composer: dropped, as Send drops
        // it.
        var framed = Frame(composer);
        lock (_batchLock)
        {
            if (framed == null)
                return true;
            // The batch closed while this was composed: it has gone already,
            // so this goes now, behind it.
            if (_batch == null)
                SendNow(framed);
            else
                _batch.Add(framed);
            return true;
        }
    }

    private bool HoldInBatch(byte[] framed)
    {
        lock (_batchLock)
        {
            if (_batch == null)
                return false;
            _batch.Add(framed);
            return true;
        }
    }

    public abstract void CreateHeader(Memory<byte> memory, uint messageId);
}
