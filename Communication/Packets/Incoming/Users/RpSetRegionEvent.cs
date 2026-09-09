using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the player picked their region in the phone's Settings > General.
///
/// Only the three codes the picker offers are accepted, plus '' for clearing
/// it - anything else is dropped rather than stored, because this string ends
/// up on a profile other people read and the client is free to send whatever
/// it likes.
///
/// Broadcast to the room after saving, so a profile someone else has open
/// updates without re-asking.
/// </summary>
internal class RpSetRegionEvent : IPacketEvent
{
    private static readonly string[] Allowed = { "", "na", "eu", "oc" };

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var region = (packet.ReadString() ?? "").Trim().ToLowerInvariant();
        if (Array.IndexOf(Allowed, region) < 0)
            return Task.CompletedTask;

        habbo.RpRegion = region;
        habbo.SaveKey("rp_region", region);

        var composer = new RpUserRegionComposer(habbo.Id, region);
        session.Send(composer);
        habbo.CurrentRoom?.SendPacket(composer);
        return Task.CompletedTask;
    }
}
