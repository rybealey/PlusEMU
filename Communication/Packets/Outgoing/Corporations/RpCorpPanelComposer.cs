using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: who is looking and at which corporation - the
/// answer to opening it. Capabilities are CorpPanel.Capability bits (the
/// server checks each action again); the week is when it started and when it
/// resets (Sunday 11:59 PM UK), as unix seconds.
/// </summary>
public class RpCorpPanelComposer : IServerPacket
{
    private readonly int _corpId;
    private readonly string _corpName;
    private readonly string _badge;
    private readonly string _rankName;
    private readonly int _capabilities;
    private readonly int _shiftQuota;
    private readonly int _weekStartedAt;
    private readonly int _weekResetAt;
    private readonly int _pendingPromotions;
    private readonly int _pendingDemotions;

    public uint MessageId => ServerPacketHeader.RpCorpPanelComposer;

    public RpCorpPanelComposer(int corpId, string corpName, string badge, string rankName, int capabilities, int shiftQuota, int weekStartedAt, int weekResetAt,
        int pendingPromotions, int pendingDemotions)
    {
        _corpId = corpId;
        _corpName = corpName ?? "";
        _badge = badge ?? "";
        _rankName = rankName ?? "";
        _capabilities = capabilities;
        _shiftQuota = shiftQuota;
        _weekStartedAt = weekStartedAt;
        _weekResetAt = weekResetAt;
        _pendingPromotions = pendingPromotions;
        _pendingDemotions = pendingDemotions;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_corpId);
        packet.WriteString(_corpName);
        packet.WriteString(_badge);
        packet.WriteString(_rankName);
        packet.WriteInteger(_capabilities);
        packet.WriteInteger(_shiftQuota);
        packet.WriteInteger(_weekStartedAt);
        packet.WriteInteger(_weekResetAt);
        // slice 3: open requests, for the sidebar's counts
        packet.WriteInteger(_pendingPromotions);
        packet.WriteInteger(_pendingDemotions);
    }
}
