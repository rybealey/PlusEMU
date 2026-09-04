using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the gang invites waiting on a player who is NOT in a gang - the
/// create view shows them above the founding form. Sent as the Gang window's
/// state when the viewer has no gang, and pushed when an invite for them is
/// created, cancelled or voided.
/// </summary>
public class RpGangInvitesComposer : IServerPacket
{
    public record Invite(int GangId, string Name, string ColourA, string ColourB, string InvitedBy, int ExpiresAt);

    private readonly List<Invite> _invites;

    public uint MessageId => ServerPacketHeader.RpGangInvitesComposer;

    public RpGangInvitesComposer(List<Invite> invites)
    {
        _invites = invites;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_invites.Count);
        foreach (var invite in _invites)
        {
            packet.WriteInteger(invite.GangId);
            packet.WriteString(invite.Name ?? "");
            packet.WriteString(invite.ColourA ?? "");
            packet.WriteString(invite.ColourB ?? "");
            packet.WriteString(invite.InvitedBy ?? "");
            packet.WriteInteger(invite.ExpiresAt);
        }
    }
}
