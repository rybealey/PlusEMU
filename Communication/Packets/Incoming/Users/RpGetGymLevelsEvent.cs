using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Gym;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp gym: a profile asking for a player's Strength and Stamina levels.
/// Anybody's - everyone can see them (Twist's call).
/// </summary>
internal class RpGetGymLevelsEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null)
            return Task.CompletedTask;

        var userId = packet.ReadInt();
        if (userId <= 0)
            return Task.CompletedTask;

        var (strength, stamina) = GymState.LevelsOf(userId);
        session.Send(new RpGymLevelsComposer(userId, strength, stamina));
        return Task.CompletedTask;
    }
}
