using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the Gang window asks for its state - RpGangDetailComposer when
/// the player is in a gang, otherwise RpGangInvitesComposer (the invites
/// waiting on them, shown above the founding form).
/// </summary>
internal class RpGetGangDetailEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null)
            return Task.CompletedTask;
        GangManager.SendState(session);
        return Task.CompletedTask;
    }
}
