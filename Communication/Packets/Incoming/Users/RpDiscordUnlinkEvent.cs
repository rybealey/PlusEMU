using Plus.HabboHotel.Discord;
using Plus.HabboHotel.GameClients;
using Plus.Communication.Packets.Outgoing.Users;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the player disconnected their Discord account from the Settings
/// window. The link is cleared here and now; the CMS scheduler strips the
/// Discord roles when it drains the queue.
/// </summary>
internal class RpDiscordUnlinkEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null)
            return Task.CompletedTask;

        DiscordSyncUtility.Unlink(session.GetHabbo().Id);

        // Answer with live state either way - an already-unlinked account and
        // a failed write both correctly report the current truth.
        var state = DiscordSyncUtility.GetLinkState(session.GetHabbo().Id);
        session.Send(new RpDiscordStatusComposer(!string.IsNullOrEmpty(state.DiscordId), state.DiscordLinkedAt));

        return Task.CompletedTask;
    }
}
