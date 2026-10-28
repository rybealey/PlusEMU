using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the client asks for one user's gang membership (its own id for
/// the Gang window's create-vs-member gate, a target's id on profile open).
/// Answered with RpUserGangComposer; gangId 0 = not in a gang.
/// </summary>
internal class RpGetUserGangEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        if (session.GetHabbo() == null || userId <= 0)
            return Task.CompletedTask;
        if (GangUtility.TryGetGangShown(userId, out var gang))
        {
            session.Send(GangUtility.ComposeFor(userId, gang));
            return Task.CompletedTask;
        }
        // pixelrp (Twist, 2026-10-28): not remembered, so read from the database
        // on RoomTickWriter's read line, not here - this player's
        // packets are handled one at a time, and the walk or click after this
        // one waited on the read. Nothing is skipped or limited: the answer
        // comes once it is read, and every gang reply names its player, so one
        // that lands after a later reply is filed under the right one.
        RoomTickWriter.QueueRead(() => session.Send(GangUtility.ComposeFor(userId, GangUtility.GetGangShown(userId))));
        return Task.CompletedTask;
    }
}
