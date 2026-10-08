using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>
/// pixelrp City Panel: the Economy tab - every corporation with its ranks'
/// pay and who is on shift, then the service prices. `notice` is what to tell
/// the staff member about their last change.
/// </summary>
public class RpCityEconomyComposer : IServerPacket
{
    private readonly List<CityEconomy.Corp> _corps;
    private readonly List<ServicePrices.Price> _prices;
    private readonly string _notice;

    public uint MessageId => ServerPacketHeader.RpCityEconomyComposer;

    public RpCityEconomyComposer(List<CityEconomy.Corp> corps, List<ServicePrices.Price> prices, string notice = "")
    {
        _corps = corps;
        _prices = prices;
        _notice = notice ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_corps.Count);
        foreach (var corp in _corps)
        {
            packet.WriteInteger(corp.Id);
            packet.WriteString(corp.Name);
            packet.WriteInteger(corp.Ranks.Count);
            foreach (var rank in corp.Ranks)
            {
                packet.WriteInteger(rank.Id);
                packet.WriteString(rank.Name);
                packet.WriteInteger(rank.Pay);
            }
            packet.WriteInteger(corp.OnShift.Count);
            foreach (var (userId, username, rankName) in corp.OnShift)
            {
                packet.WriteInteger(userId);
                packet.WriteString(username);
                packet.WriteString(rankName);
            }
            // Settings.
            packet.WriteBoolean(corp.Hidden);
        }
        packet.WriteInteger(_prices.Count);
        foreach (var price in _prices)
        {
            packet.WriteString(price.Key);
            packet.WriteString(price.Name);
            packet.WriteInteger(price.CorporationId);
            packet.WriteInteger(price.Amount);
        }
        packet.WriteString(_notice);
    }
}
