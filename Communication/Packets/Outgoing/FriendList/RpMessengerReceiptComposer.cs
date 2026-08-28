using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.FriendList;

/// <summary>
/// pixelrp: message receipt for the phone's Messages app. Tells a sender
/// that the friend either received their messages (delivered — friend was
/// online when the message went through, or their offline messages were
/// flushed at login) or read them (the friend opened the conversation).
/// Receipts are live-only; nothing is persisted.
/// </summary>
public class RpMessengerReceiptComposer : IServerPacket
{
    public const int Delivered = 1;
    public const int Read = 2;
    public const int NotDelivered = 3; // pixelrp: recipient has airplane mode on

    private readonly int _friendId;
    private readonly int _type;

    public uint MessageId => ServerPacketHeader.RpMessengerReceiptComposer;

    public RpMessengerReceiptComposer(int friendId, int type)
    {
        _friendId = friendId;
        _type = type;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_friendId);
        packet.WriteInteger(_type);
    }
}
