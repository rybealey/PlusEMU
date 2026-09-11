using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Birthdays;
using Plus.HabboHotel.Users.Privacy;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: ask for a birthday - the player's own (no payload / 0) for the
/// phone's Account screen, or another user's by id for their profile card.
///
/// Somebody else's is subject to their Privacy screen, and a birthday this
/// asker may not see comes back as 0/0 - the same answer as one that was never
/// filled in. The profile draws nothing either way, so hidden and unset are
/// indistinguishable from outside, which is what hiding something has to mean.
/// </summary>
internal class RpGetBirthdayEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        var userId = packet.HasDataRemaining() ? packet.ReadInt() : 0;
        var target = (userId > 0) ? userId : habbo.Id;

        if (!PrivacyUtility.CanSeeBirthday(habbo.Id, target))
        {
            session.Send(new Outgoing.Users.RpBirthdayComposer(target, 0, 0));
            return Task.CompletedTask;
        }

        BirthdayUtility.SendBirthday(session, target);
        return Task.CompletedTask;
    }
}
