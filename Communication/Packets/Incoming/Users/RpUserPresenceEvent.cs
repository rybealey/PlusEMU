using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: whether a player is online, asked by :t and :lt when the name is
/// not in the asker's room - the client only knows its own room. Online, they
/// are targeted from wherever they are (their bars then come through
/// RpGetUserStatsEvent); never told which room that is.
/// </summary>
internal class RpUserPresenceEvent : IPacketEvent
{
    private readonly IGameClientManager _gameClientManager;

    public RpUserPresenceEvent(IGameClientManager gameClientManager) => _gameClientManager = gameClientManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var name = packet.ReadString().Trim();
        if (session.GetHabbo() == null || name.Length == 0 || name.Length > 50)
            return Task.CompletedTask;
        // Answered with the name as asked, so the client can match it to its question.
        session.Send(new RpUserPresenceComposer(name, _gameClientManager.GetClientByUsername(name)?.GetHabbo()));
        return Task.CompletedTask;
    }
}
