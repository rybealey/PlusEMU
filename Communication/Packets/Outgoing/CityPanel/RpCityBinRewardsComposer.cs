using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>
/// pixelrp City Panel: the Economy tab's Bin Search - every reward row (id,
/// reward, weight, min, max), then what a row can be (key, name), then what
/// to tell the staff member about their last change.
/// </summary>
public class RpCityBinRewardsComposer : IServerPacket
{
    private readonly List<BinRewards.Reward> _rewards;
    private readonly List<(string Key, string Name)> _options;
    private readonly string _notice;

    public uint MessageId => ServerPacketHeader.RpCityBinRewardsComposer;

    public RpCityBinRewardsComposer(string notice = "")
    {
        _rewards = BinRewards.All();
        _options = BinRewards.Options();
        _notice = notice ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_rewards.Count);
        foreach (var reward in _rewards)
        {
            packet.WriteInteger(reward.Id);
            packet.WriteString(reward.Key);
            packet.WriteInteger(reward.Weight);
            packet.WriteInteger(reward.Min);
            packet.WriteInteger(reward.Max);
        }
        packet.WriteInteger(_options.Count);
        foreach (var (key, name) in _options)
        {
            packet.WriteString(key);
            packet.WriteString(name);
        }
        packet.WriteString(_notice);
    }
}
