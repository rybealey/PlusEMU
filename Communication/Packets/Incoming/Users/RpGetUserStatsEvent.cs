using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: your HUD target's health, energy and aggression while they are in
/// another room - a room's bars only go to the people in it, and the HUD keeps
/// its target through room changes. The client asks once a second, only then.
///
/// Only for the player's own HUD target (Habbo.RpHudTargetId, set by
/// RpSetHudTargetEvent), so nobody can read anybody else's bars from anywhere.
/// </summary>
internal class RpGetUserStatsEvent : IPacketEvent
{
    private readonly IGameClientManager _gameClientManager;

    public RpGetUserStatsEvent(IGameClientManager gameClientManager) => _gameClientManager = gameClientManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || userId <= 0 || userId != habbo.RpHudTargetId)
            return Task.CompletedTask;
        var target = _gameClientManager.GetClientByUserId(userId)?.GetHabbo();
        if (target == null)
        {
            session.Send(RpUserStatsComposer.Offline(userId));
            return Task.CompletedTask;
        }
        target.EnsureRpStatsLoaded();
        session.Send(new RpUserStatsComposer(target));
        return Task.CompletedTask;
    }
}
