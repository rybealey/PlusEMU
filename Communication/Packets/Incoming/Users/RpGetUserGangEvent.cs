using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

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
        session.Send(GangUtility.ComposeFor(userId, GangUtility.GetGang(userId)));
        return Task.CompletedTask;
    }
}
