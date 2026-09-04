using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: one player's gang membership, keyed by user id. gangId 0 = not
/// in a gang (clears client-side). Colours travel as '#rrggbb' strings
/// (gangs store raw RGB ints in groups.colour1/colour2). gangCost rides
/// along so the Gang window can price the Create button without a second
/// packet. Sent on profile open / window open, and broadcast hotel-wide on
/// every gang mutation so open profiles update in real-time.
/// </summary>
public class RpUserGangComposer : IServerPacket
{
    private readonly int _userId;
    private readonly int _gangId;
    private readonly string _name;
    private readonly string _colourA;
    private readonly string _colourB;
    private readonly bool _isOwner;
    private readonly int _gangCost;

    public uint MessageId => ServerPacketHeader.RpUserGangComposer;

    public RpUserGangComposer(int userId, int gangId, string name, string colourA, string colourB, bool isOwner, int gangCost)
    {
        _userId = userId;
        _gangId = gangId;
        _name = name;
        _colourA = colourA;
        _colourB = colourB;
        _isOwner = isOwner;
        _gangCost = gangCost;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_userId);
        packet.WriteInteger(_gangId);
        packet.WriteString(_name ?? "");
        packet.WriteString(_colourA ?? "");
        packet.WriteString(_colourB ?? "");
        packet.WriteInteger(_isOwner ? 1 : 0);
        packet.WriteInteger(_gangCost);
    }
}
