using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the player clicked the x on their HUD passive tag to end their
/// passive status early. Announced with a bubble-27 shout.
/// </summary>
internal class RpPassiveCancelEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        habbo.EnsureRpStatsLoaded();
        if (habbo.RpPassiveSeconds <= 0)
            return Task.CompletedTask;
        habbo.RpPassiveSeconds = 0;
        habbo.RpPassiveLastTick = 0;
        habbo.SaveRpStats();
        var roomUser = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (roomUser == null)
            return Task.CompletedTask;
        // blue action bubble, same as :passive off and the fight commands
        roomUser.OnChat(4, "*discovers newfound anger, eliminating their passive state*", true);
        habbo.CurrentRoom.SendPacket(new RpStatsComposer(roomUser.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax, (int)Math.Round(habbo.RpAggression), 0, habbo.Rank >= 5 ? 1 : 0));
        // pixelrp: drop the passive enable if it (and only it) is showing.
        if (habbo.Effects != null && habbo.Effects.CurrentEffect == Habbo.PassiveEnableEffectId)
            habbo.Effects.ApplyEffect(0);
        return Task.CompletedTask;
    }
}
