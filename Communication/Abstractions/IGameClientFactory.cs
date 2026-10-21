using Plus.Utilities.DependencyInjection;

namespace Plus.Communication.Abstractions;

[Singleton]
public interface IGameClientFactory
{
}

public interface IGameClientFactory<TGameClient, TServer> : IGameClientFactory
{
    TGameClient Create(TServer server);
}