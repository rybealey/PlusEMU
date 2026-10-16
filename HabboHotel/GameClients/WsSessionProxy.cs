using NetCoreServer;
using NLog;

namespace Plus.HabboHotel.GameClients;

public class WsSessionProxy : WsSession
{
    private static readonly ILogger Log = LogManager.GetLogger("Plus.HabboHotel.GameClients.WsSessionProxy");

    /// <summary>
    /// pixelrp: the most that may wait unsent for one client. Unset (0, no
    /// limit), a connection that had stopped reading kept every packet sent to
    /// it in memory for as long as it stayed open. 8 MB is far more than a room
    /// load or a long lag spike leaves queued for a client that is reading.
    /// </summary>
    private const int SendBufferLimit = 8 * 1024 * 1024;

    private readonly GameClient _client;
    private int _overLimit;

    // pixelrp: bytes in since the last whole WebSocket message. The socket library puts a
    // message back together in full before handing it over, so this holds a message to the
    // same limit as a packet while it does. Only touched on this session's receive thread.
    private long _bytesSinceMessage;

    public WsSessionProxy(WsServer server, GameClient client) : base(server)
    {
        _client = client;
        OptionSendBufferLimit = SendBufferLimit;
        _client.SendCallback = args =>
        {
            if (!Socket.Connected) return false;
            var buffer = args.MemoryBuffer.ToArray();
            // pixelrp: the args only carried the bytes this far, and they are
            // copied out now - disposed here rather than left to the finalizer,
            // one per packet.
            args.Dispose();
            if (SendBinaryAsync(buffer, 0, buffer.Length))
                return true;
            // pixelrp: refused while still connected means the limit above was
            // hit, and this packet is lost. A client that misses packets is
            // silently out of step with the hotel, so the connection is closed
            // instead. Off this thread, and only once: a send can be deep
            // inside a room broadcast, and a logout must not run in the middle
            // of one.
            if (IsConnected && Interlocked.Exchange(ref _overLimit, 1) == 0)
            {
                Log.Warn($"Session {Id} has {BytesPending} bytes waiting to send (limit {SendBufferLimit}) - disconnecting.");
                Task.Run(() => Disconnect());
            }
            return false;
        };
        _client.DisconnectRequested = () => Disconnect();
    }

    protected override void OnConnected()
    {
        base.OnConnected();
    }

    protected override void OnDisconnected() => _client.OnDisconnected();

    // pixelrp: raw bytes off the socket, before the WebSocket layer. Held to the same limit
    // as a packet (GameClient.MaxBufferedBytes), and nothing thrown in here may escape the
    // receive thread.
    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            _bytesSinceMessage += size;
            if (_bytesSinceMessage > GameClient.MaxBufferedBytes)
            {
                Log.Warn($"Session {Id} sent {_bytesSinceMessage} bytes without finishing a WebSocket message - disconnecting.");
                Disconnect();
                return;
            }
            base.OnReceived(buffer, offset, size);
        }
        catch (Exception e)
        {
            Log.Error(e, $"WebSocket receive failed for session {Id} - disconnecting.");
            Disconnect();
        }
    }

    public override void OnWsReceived(byte[] buffer, long offset, long size)
    {
        _bytesSinceMessage = 0;
        _client.OnReceived(buffer, offset, size);
    }
}