using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: one player's employment state, keyed by user id. corpId 0 =
/// unemployed (clears client-side). Sent on room entry for everyone in the
/// room, on profile open, and broadcast on hire so infostands and open
/// profiles update in real-time.
/// </summary>
public class RpUserCorpComposer : IServerPacket
{
    private readonly int _userId;
    private readonly int _corpId;
    private readonly string _badge;
    private readonly string _corpName;
    private readonly string _rankName;
    private readonly int _tier;

    public uint MessageId => ServerPacketHeader.RpUserCorpComposer;

    public RpUserCorpComposer(int userId, int corpId, string badge, string corpName, string rankName, int tier)
    {
        _userId = userId;
        _corpId = corpId;
        _badge = badge;
        _corpName = corpName;
        _rankName = rankName;
        _tier = tier;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_userId);
        packet.WriteInteger(_corpId);
        packet.WriteString(_badge ?? "");
        packet.WriteString(_corpName ?? "");
        packet.WriteString(_rankName ?? "");
        packet.WriteInteger(_tier);
    }
}
