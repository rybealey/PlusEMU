using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Messenger;

namespace Plus.Communication.Packets.Outgoing.FriendList;

public class HabboSearchResultComposer : IServerPacket
{
    private readonly List<SearchResult> _friends;
    private readonly List<SearchResult> _otherUsers;
    public uint MessageId => ServerPacketHeader.HabboSearchResultComposer;

    public HabboSearchResultComposer(List<SearchResult> friends, List<SearchResult> otherUsers)
    {
        _friends = friends;
        _otherUsers = otherUsers;
    }

    // pixelrp: the look goes out for offline players too (it used to be
    // blanked), so the gang Invite tab draws them instead of an empty avatar.
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_friends.Count);
        foreach (var friend in _friends.ToList())
        {
            var online = PlusEnvironment.Game.ClientManager.GetClientByUserId(friend.UserId) != null;
            packet.WriteInteger(friend.UserId);
            packet.WriteString(friend.Username);
            packet.WriteString(friend.Motto);
            packet.WriteBoolean(online);
            packet.WriteBoolean(false);
            packet.WriteString(string.Empty);
            packet.WriteInteger(0);
            packet.WriteString(friend.Figure);
            packet.WriteString(friend.LastOnline);
        }
        packet.WriteInteger(_otherUsers.Count);
        foreach (var otherUser in _otherUsers.ToList())
        {
            var online = PlusEnvironment.Game.ClientManager.GetClientByUserId(otherUser.UserId) != null;
            packet.WriteInteger(otherUser.UserId);
            packet.WriteString(otherUser.Username);
            packet.WriteString(otherUser.Motto);
            packet.WriteBoolean(online);
            packet.WriteBoolean(false);
            packet.WriteString(string.Empty);
            packet.WriteInteger(0);
            packet.WriteString(otherUser.Figure);
            packet.WriteString(otherUser.LastOnline);
        }

    }
}