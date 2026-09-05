using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Birthdays;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: ask for a birthday - the player's own (no payload / 0) for the
/// phone's Account screen, or another user's by id for their profile card.
/// </summary>
internal class RpGetBirthdayEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        var userId = packet.HasDataRemaining() ? packet.ReadInt() : 0;
        BirthdayUtility.SendBirthday(session, userId > 0 ? userId : habbo.Id);
        return Task.CompletedTask;
    }
}
