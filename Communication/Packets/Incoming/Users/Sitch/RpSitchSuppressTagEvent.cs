using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>
/// pixelrp: take a tag out of circulation, or put it back.
///
/// Gated on rp_sitch_moderate - the same permission that removes somebody
/// else's post, because this is the same kind of decision made at the level of
/// a word rather than a post. The client hides the button without it; this
/// check is the actual gate.
///
/// Nothing is deleted. The posts stand, the tag rows stand, and lifting the
/// suppression puts everything back. Staff decide what the city amplifies, not
/// what it said.
/// </summary>
internal class RpSitchSuppressTagEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var tag = (packet.ReadString() ?? "").TrimStart('#').Trim();
        var on = packet.ReadInt() == 1;
        var habbo = session.GetHabbo();
        if (habbo == null) return Task.CompletedTask;

        if (!(habbo.Permissions?.HasCommand("rp_sitch_moderate") ?? false))
        {
            session.SendWhisper("You cannot do that.");
            return Task.CompletedTask;
        }

        if (tag.Length == 0) return Task.CompletedTask;

        SitchUtility.SetTagSuppressed(tag, habbo.Id, on);
        // Answer with the list they are looking at, so the row leaves (or comes
        // back) without a reopen.
        SitchUtility.SendTrending(session);
        return Task.CompletedTask;
    }
}
