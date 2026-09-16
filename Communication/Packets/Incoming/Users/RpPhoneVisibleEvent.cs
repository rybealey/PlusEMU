using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the phone was opened or closed on screen, so the player's avatar
/// picks up or puts away handitem 244.
///
/// Deliberately NOT persisted. "My phone is open right now" is a fact about a
/// client that is currently connected, and a flag surviving a crash would leave
/// a player holding a phone they cannot put down. The client tells us on every
/// change, and it starts closed, so the truth is re-established at login for
/// free.
///
/// Nothing is validated beyond the boolean, because there is nothing to
/// validate: the worst a forged packet achieves is holding or not holding a
/// phone, which the sender could do by tapping the toolbar anyway.
/// </summary>
internal class RpPhoneVisibleEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var open = packet.ReadBool();
        habbo.PhoneOpen = open;

        // Not being in a room is normal - the phone opens in the hotel view
        // too. The flag is what matters; the hand catches up on room entry.
        var room = habbo.CurrentRoom;
        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);

        user?.SetPhoneInHand(open);

        return Task.CompletedTask;
    }
}
