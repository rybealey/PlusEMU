using System.Globalization;
using System.Text;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

public class UserUpdateComposer : IServerPacket, IUnitPacket
{
    private readonly ICollection<RoomUser> _users;
    public uint MessageId => ServerPacketHeader.UserUpdateComposer;

    // pixelrp :invisible: the same list without anyone the viewer can't see.
    public IServerPacket? WithoutUnits(ISet<int> hidden)
    {
        if (!_users.Any(user => hidden.Contains(user.VirtualId)))
            return this;
        var visible = _users.Where(user => !hidden.Contains(user.VirtualId)).ToList();
        return visible.Count == 0 ? null : new UserUpdateComposer(visible);
    }

    public UserUpdateComposer(ICollection<RoomUser> users)
    {
        _users = users;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_users.Count);
        foreach (var user in _users.ToList())
        {
            packet.WriteInteger(user.VirtualId);
            packet.WriteInteger(user.X);
            packet.WriteInteger(user.Y);
            packet.WriteString(user.Z.ToString(CultureInfo.InvariantCulture));
            packet.WriteInteger(user.RotHead);
            packet.WriteInteger(user.RotBody);
            var statusComposer = new StringBuilder();
            statusComposer.Append("/");
            foreach (var status in user.Statusses.ToList())
            {
                statusComposer.Append(status.Key);
                if (!string.IsNullOrEmpty(status.Value))
                {
                    statusComposer.Append(" ");
                    statusComposer.Append(status.Value);
                }
                statusComposer.Append("/");
            }
            statusComposer.Append("/");
            packet.WriteString(statusComposer.ToString());
        }
    }
}