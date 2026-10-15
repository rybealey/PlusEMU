using Microsoft.Extensions.Options;
using NetCoreServer;
using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;
using System.Collections.Concurrent;
using Plus.Communication.Flash;

namespace Plus.Communication.Abstractions;

public abstract class TcpGameServer<TGameServerOptions> : TcpServer, IGameServer
    where TGameServerOptions : class, IGameServerOptions
{
    private readonly IGameClientFactory<TcpSessionProxy, TcpServer> _clientFactory;
    private readonly IPacketManager _packetManager;
    private readonly ConcurrentDictionary<Guid, TcpSession> _connectedClients = new();

    protected TcpGameServer(IOptions<TGameServerOptions> options,
        IGameClientFactory<TcpSessionProxy, TcpServer> clientFactory,
        IPacketManager packetManager) : base(options.Value.Hostname,
        options.Value.Port)
    {
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

    protected override TcpSession CreateSession() => _clientFactory.Create(this);

    protected override void OnConnected(TcpSession session)
    {
        if (session is not TcpSessionProxy gameClient)
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

public interface IGameClientFactory<TGameClient, TServer> : IGameClientFactory
{
    TGameClient Create(TServer server);
}