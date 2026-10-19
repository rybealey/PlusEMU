using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: how many hits the player's vape has left, and how many it holds
/// (RpVape.Hits) - the green bar on the vape in the backpack, as the stun gun
/// has. Sent at login, after every hit, and when a vape coming into an empty
/// backpack starts full. The count belongs to the player, not the item, so it
/// is sent whether or not they hold a vape right now; the client only draws it
/// on a vape.
/// </summary>
public class RpVapeChargeComposer : IServerPacket
{
    private readonly int _left;
    private readonly int _max;

    public uint MessageId => ServerPacketHeader.RpVapeChargeComposer;

    public RpVapeChargeComposer(int left, int max)
    {
        _left = left;
        _max = max;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_left);
        packet.WriteInteger(_max);
    }
}
