using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NetCoreServer;
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