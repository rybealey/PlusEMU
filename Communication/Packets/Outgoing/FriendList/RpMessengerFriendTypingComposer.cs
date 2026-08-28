using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.FriendList;

/// <summary>
/// pixelrp: tells a client that a friend is (or has stopped) typing a message
/// to them in the phone's Messages app. Live-only; nothing is persisted, and
/// the receiving client also times the indicator out on its own if a stop
/// packet never arrives.
/// </summary>
public class RpMessengerFriendTypingComposer : IServerPacket
{
    private readonly int _friendId;
    private readonly bool _typing;

    public uint MessageId => ServerPacketHeader.RpMessengerFriendTypingComposer;

    public RpMessengerFriendTypingComposer(int friendId, bool typing)
    {
        _friendId = friendId;
        _typing = typing;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_friendId);
        packet.WriteBoolean(_typing);
    }
}
