using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Privacy;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the player picked their region in the phone's Settings > General.
///
/// Only the three codes the picker offers are accepted, plus '' for clearing
/// it - anything else is dropped rather than stored, because this string ends
/// up on a profile other people read and the client is free to send whatever
/// it likes.
///
/// Told to the room after saving, so a profile someone else has open updates
/// without re-asking - but PER PERSON, not as one broadcast: whoever may not
/// see this region is told it is empty, which is what they would have got by
/// asking. One packet to the whole room would hand it to everybody present.
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

        session.Send(new RpUserRegionComposer(habbo.Id, region));

        var room = habbo.CurrentRoom;
        if (room == null)
            return Task.CompletedTask;

        var shared = new RpUserRegionComposer(habbo.Id, region);
        var hidden = new RpUserRegionComposer(habbo.Id, "");
        foreach (var user in room.GetRoomUserManager().GetRoomUsers())
        {
            var client = user?.GetClient();
            var viewer = client?.GetHabbo();
            if (viewer == null || viewer.Id == habbo.Id)
                continue;
            client.Send(PrivacyUtility.CanSeeRegion(viewer.Id, habbo.Id) ? shared : hidden);
        }

        return Task.CompletedTask;
    }
}
