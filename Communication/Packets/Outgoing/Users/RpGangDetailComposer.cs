using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the viewer's own gang in full for the Gang window - identity,
/// level, THEIR permission bits (GangManager.Perm*), the custom roles in
/// display order, every member (roleId 0 = plain Member) and, when they may
/// act on them, the pending invites. Sent on request and pushed to every
/// online member after any gang mutation.
/// </summary>
public class RpGangDetailComposer : IServerPacket
{
    public record Role(int Id, string Name, int Order, int Flags);

    public record Member(int UserId, string Username, string Figure, int RoleId, bool Online, int JoinedAt);

    public record Invite(int UserId, string Username, string Figure, string InvitedBy, int ExpiresAt);

    private readonly int _gangId;
    private readonly string _name;
    private readonly string _colourA;
    private readonly string _colourB;
    private readonly int _ownerId;
    private readonly string _ownerName;
    private readonly int _level;
    private readonly int _xp;
    private readonly int _xpCap;
    private readonly int _createdAt;
    private readonly int _permissions;
    private readonly List<Role> _roles;
    private readonly List<Member> _members;
    private readonly List<Invite> _invites;
    private readonly int _inviteHours;

    public uint MessageId => ServerPacketHeader.RpGangDetailComposer;

    public RpGangDetailComposer(int gangId, string name, string colourA, string colourB, int ownerId, string ownerName, int level, int xp, int xpCap, int createdAt,
        int permissions, List<Role> roles, List<Member> members, List<Invite> invites, int inviteHours)
    {
        _gangId = gangId;
        _name = name;
        _colourA = colourA;
        _colourB = colourB;
        _ownerId = ownerId;
        _ownerName = ownerName;
        _level = level;
        _xp = xp;
        _xpCap = xpCap;
        _createdAt = createdAt;
        _permissions = permissions;
        _roles = roles;
        _members = members;
        _invites = invites;
        _inviteHours = inviteHours;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_gangId);
        packet.WriteString(_name ?? "");
        packet.WriteString(_colourA ?? "");
        packet.WriteString(_colourB ?? "");
        packet.WriteInteger(_ownerId);
        packet.WriteString(_ownerName ?? "");
        packet.WriteInteger(_level);
        packet.WriteInteger(_xp);
        packet.WriteInteger(_xpCap);
        packet.WriteInteger(_createdAt);
        packet.WriteInteger(_permissions);
        packet.WriteInteger(_roles.Count);
        foreach (var role in _roles)
        {
            packet.WriteInteger(role.Id);
            packet.WriteString(role.Name ?? "");
            packet.WriteInteger(role.Order);
            packet.WriteInteger(role.Flags);
        }
        packet.WriteInteger(_members.Count);
        foreach (var member in _members)
        {
            packet.WriteInteger(member.UserId);
            packet.WriteString(member.Username ?? "");
            packet.WriteString(member.Figure ?? "");
            packet.WriteInteger(member.RoleId);
            packet.WriteInteger(member.Online ? 1 : 0);
            packet.WriteInteger(member.JoinedAt);
        }
        packet.WriteInteger(_invites.Count);
        foreach (var invite in _invites)
        {
            packet.WriteInteger(invite.UserId);
            packet.WriteString(invite.Username ?? "");
            packet.WriteString(invite.Figure ?? "");
            packet.WriteString(invite.InvitedBy ?? "");
            packet.WriteInteger(invite.ExpiresAt);
        }
        packet.WriteInteger(_inviteHours);
    }
}
