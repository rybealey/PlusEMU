using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the Gang window asks for its state - RpGangDetailComposer when
/// the player is in a gang, otherwise RpGangInvitesComposer (the invites
/// waiting on them, shown above the founding form). With a gang id in the
/// payload it answers with that gang's detail instead (read-only view).
/// </summary>
internal class RpGetGangDetailEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        // gangId > 0: a read-only look at ANOTHER gang (a target's crest, a
        // profile's gang card). Composed for the viewer as usual, so a
        // non-member gets no permission bits and no invites.
        var gangId = packet.HasDataRemaining() ? packet.ReadInt() : 0;
        if (gangId > 0)
        {
            var snapshot = GangManager.Load(gangId);
            if (snapshot != null)
                session.Send(GangManager.Compose(snapshot, habbo.Id));
            return Task.CompletedTask;
        }

        GangManager.SendState(session);
        return Task.CompletedTask;
    }
}
