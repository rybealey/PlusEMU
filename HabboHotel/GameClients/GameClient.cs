using System.Net.Sockets;
using Microsoft.IO;
using NLog;
using Plus.Communication.Encryption.Crypto.Prng;
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

    public RecyclableMemoryStream? _incompleteStream;
    public Arc4? Rc4Client { get; set; }

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


    public void Disconnect() => DisconnectRequested?.Invoke();

    protected GameClient(IGameServer server, IPacketFactory packetFactory)
    {
        _packetFactory = packetFactory;
        _server = server;
    }

    internal void OnDisconnected() => _habbo?.OnDisconnect();

    /// <summary>
    /// Upper bound on a single incoming frame. Real client packets are a few hundred bytes at
    /// most; this only exists to bound what a hostile client can make us buffer.
    /// </summary>
    private const int MaxPacketLength = 1024 * 1024;

    internal abstract (bool Complete, uint MessageId, int HeaderLength, int Length) GetMessageIdAndPacketLength(ReadOnlyMemory<byte> buffer);
    internal virtual async void OnReceived(byte[] buffer, long offset, long size)
    {
        if (size > int.MaxValue) throw new InvalidOperationException("");
        await using var stream = PlusMemoryStream.GetStream(buffer.AsSpan().Slice((int) offset, (int) size));
        var memory = stream.GetMemory().Slice(0, (int)stream.Length);

        if (_incompleteStream != null)
        {
            _incompleteStream.Write(memory.Span);
            memory = _incompleteStream.GetMemory().Slice(0, (int)_incompleteStream.Length);
        }

        while (memory.Length > 0)
        {
            var (complete, messageId, headerLength, length) = GetMessageIdAndPacketLength(memory);

            // The frame length comes straight off the wire, so an injection tool can send
            // any value it likes. A negative one makes the slices below throw out of this
            // async void method - an unhandled exception that takes the whole emulator down,
            // not just this session. There is no way to resynchronise on a bad length either
            // (we cannot find where the next frame starts), so drop the connection.
            if (complete && (length < 0 || headerLength < 0 || length > MaxPacketLength))
            {
                Log.Warn($"Malformed packet frame from session {Id} (message {messageId}, length {length}) - disconnecting.");
                Disconnect();
                return;
            }

            if (!complete)
            {
                // An oversized length is reported as "incomplete" (the bytes never arrive),
                // so without this cap a client could claim a huge frame and grow this buffer
                // until the process runs out of memory.
                if (memory.Length > MaxPacketLength)
                {
                    Log.Warn($"Session {Id} buffered {memory.Length} bytes without completing a packet - disconnecting.");
                    Disconnect();
                    return;
                }
                _incompleteStream ??= PlusMemoryStream.GetStream(memory.Span);
                break;
            }

            try
            {
                if (Revision.IncomingIdToInternalIdMapping.TryGetValue(messageId, out var internalMessageId))
                {
                    await _server.PacketReceived(this, internalMessageId, _packetFactory.CreateIncomingPacket(memory.Slice(headerLength, length)));
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
            memory = memory.Slice(headerLength + length);
            _incompleteStream?.Advance(headerLength + length);
        }

        if (memory.Length == 0)
        {
            _incompleteStream?.Dispose();
            _incompleteStream = null;
        }
    }

    public Habbo GetHabbo() => _habbo!;

    public void SetHabbo(Habbo habbo)
    {
        if (_habbo != null) throw new InvalidOperationException();
        _habbo = habbo;
    }

    /// <summary>
    /// Forward this client to a room. Always use this instead of sending RoomForwardComposer
    /// directly: it records the target as a server-authorized entry, which OpenFlatConnectionEvent
    /// requires from non-staff clients (the navigator is staff-only, so an unauthorized flat
    /// connection is an injected packet).
    /// </summary>
    public void SendRoomForward(uint roomId)
    {
        if (_habbo != null)
            _habbo.AuthorizedRoomEntryId = roomId;
        Send(new RoomForwardComposer(roomId));
    }

    public void Send(IServerPacket composer)
    {
        var outgoingMessageId = Revision.InternalIdToOutgoingIdMapping[composer.MessageId];
        var stream = PlusMemoryStream.GetStream();
        stream.Position = 0;
        var packet = _packetFactory.CreateOutgoingPacket(stream);
        composer.Compose(packet);
        var args = new SocketAsyncEventArgs();
        var memory = stream.GetBuffer().AsMemory().Slice(0, (int)stream.Length);
        CreateHeader(memory, outgoingMessageId);
        args.SetBuffer(memory);
        SendCallback(args);
        Log.Debug($"Send Packet: {composer.GetType().Name} (EmuId: {composer.MessageId}, ClientId: {outgoingMessageId})");
        stream.Dispose();
    }

    public abstract void CreateHeader(Memory<byte> memory, uint messageId);
}
