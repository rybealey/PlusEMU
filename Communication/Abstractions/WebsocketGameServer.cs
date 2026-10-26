using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NetCoreServer;
using NLog;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Abstractions;

public abstract class WebsocketGameServer<TGameServerOptions> : WsServer, IGameServer
    where TGameServerOptions : class, IGameServerOptions
{
    private readonly IGameClientFactory<WsSessionProxy, WsServer> _clientFactory;
    private readonly IPacketManager _packetManager;
    private readonly ConcurrentDictionary<Guid, WsSessionProxy> _connectedClients = new();

    private static readonly NLog.ILogger Log = LogManager.GetLogger("Plus.Communication.Abstractions.WebsocketGameServer");

    /// <summary>
    /// pixelrp: dead connections. A player whose connection drops without
    /// closing (wifi gone, a phone changing networks, a laptop shut) is never
    /// noticed here: the TCP keep-alive below only checks the hop to nginx,
    /// which stays up. So the connection stayed open - for up to an hour in
    /// the host nginx - holding one of the two game sockets nginx allows an
    /// address, and coming back on a second device or after a second drop was
    /// refused. Now every open connection is pinged at the WebSocket level
    /// every SweepEvery, and one that has sent nothing - not a packet, not an
    /// answer to a ping - for SilentLimitMs is closed, which frees nginx's
    /// slot with it. A browser answers these pings itself, below the page, so
    /// a hidden or busy tab still answers; the game's own Pong (every 20 s)
    /// counts too.
    /// </summary>
    private static readonly TimeSpan SweepEvery = TimeSpan.FromSeconds(20);

    private const long SilentLimitMs = 120_000;

    private static readonly byte[] NoPayload = Array.Empty<byte>();

    private readonly Timer _deadConnectionSweep;
    private long _lastSweepMs;

    protected WebsocketGameServer(IOptions<TGameServerOptions> options,
        IGameClientFactory<WsSessionProxy, WsServer> clientFactory,
        IPacketManager packetManager) : base(options.Value.Hostname,
        options.Value.Port)
    {
        // pixelrp: disable Nagle's algorithm on accepted sessions. The game sends
        // many tiny packets (per-step "mv" statuses, chat); letting the kernel
        // batch them adds tens of ms of latency and jitter to every update.
        OptionNoDelay = true;
        // pixelrp: TCP keep-alive on accepted sessions. A connection whose far
        // end vanished without closing it (a pulled cable, a phone changing
        // networks, a laptop put to sleep) and that has nothing in flight is
        // otherwise never noticed, and its player stays logged in. The kernel
        // probes after 60 s of silence and drops the connection after 5
        // unanswered probes 10 s apart. (Behind a proxy this checks the hop to
        // the proxy.)
        OptionKeepAlive = true;
        OptionTcpKeepAliveTime = 60;
        OptionTcpKeepAliveInterval = 10;
        OptionTcpKeepAliveRetryCount = 5;
        _clientFactory = clientFactory;
        _packetManager = packetManager;
        _deadConnectionSweep = new Timer(_ => SweepDeadConnections(), null, SweepEvery, SweepEvery);
    }

    private void SweepDeadConnections()
    {
        try
        {
            // A sweep that ran late (a long GC pause, a starved thread pool) may
            // find connections looking silent only because their bytes are still
            // queued to be read - it pings, but closes nothing this time round.
            var now = Environment.TickCount64;
            var late = _lastSweepMs != 0 && now - _lastSweepMs > 2 * (long)SweepEvery.TotalMilliseconds;
            _lastSweepMs = now;
            foreach (var session in _connectedClients.Values)
            {
                // Not upgraded yet: the login deadline (WsSessionProxy) covers those.
                if (!session.IsConnected || !session.IsWsOpen)
                    continue;
                var silentMs = session.SilentMs;
                if (!late && silentMs > SilentLimitMs)
                {
                    Log.Info($"Session {session.Id} sent nothing for {silentMs / 1000}s - disconnecting.");
                    // off the timer: a logout does database work
                    Task.Run(() => session.Disconnect());
                    continue;
                }
                session.SendPingAsync(NoPayload);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Dead-connection sweep failed.");
        }
    }

    protected override WsSession CreateSession() => _clientFactory.Create(this);

    protected override void OnConnected(TcpSession session)
    {
        if (session is not WsSessionProxy gameClient)
        {
            session.Disconnect();
            //_logger.LogWarning("Expected {TGameClient} to be connected. Got {type}", typeof(TGameClient), session.GetType());
            return;
        }

        if (!_connectedClients.TryAdd(gameClient.Id, gameClient))
        {
            //_logger.LogWarning("Failed to cache client. {id} {ip}", gameClient.Id, gameClient.Socket.RemoteEndPoint?.ToString());
            gameClient.Disconnect();
        }
    }

    protected override void OnDisconnected(TcpSession session)
    {
        _connectedClients.TryRemove(session.Id, out _);
    }


    // TODO @80O: Allow packet content to be modified before executing.
    // TODO @80O: Add hooks before & after packet execution.
    public Task PacketReceived(GameClient client, uint messageId, IIncomingPacket packet) => _packetManager.TryExecutePacket(client, messageId, packet);
}