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

        // Unlink reports the resulting state itself. A second, unguarded
        // read here would throw out of Parse on a DB blip, and PacketManager
        // disconnects the session on a faulted Parse - a player must never
        // be kicked from the game for clicking Disconnect.
        var state = DiscordSyncUtility.Unlink(session.GetHabbo().Id);

        // Null means the state is genuinely unknown; say nothing rather than
        // report a link status that might be wrong.
        if (state != null)
            session.Send(new RpDiscordStatusComposer(!string.IsNullOrEmpty(state.DiscordId), state.DiscordLinkedAt));

        return Task.CompletedTask;
    }
}
