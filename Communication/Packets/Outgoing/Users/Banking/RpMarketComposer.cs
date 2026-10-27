using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Outgoing.Users.Banking;

/// <summary>
/// pixelrp: the Marketplace as one player sees it (RpMarket.Send) - sent
/// by :mp with `open` set, and after every action, and to a seller whose item
/// sold, without it (a closed window ignores those).
///
/// Browse rows carry no seller and My listings rows no buyer: the market is
/// anonymous (Twist, 2026-10-25). `canSell` is whether they have a bank
/// account, which selling needs. Then the note on their last action, the
/// items that can never be listed (PoliceUtility.PoliceGear, greyed on the
/// Sell tab), the server's clock, for "listed today", and how long a listing is
/// for sale (RpMarket.ListingSeconds), so My listings can say when one ends and
/// mark the expired ones.
/// </summary>
public class RpMarketComposer : IServerPacket
{
    private readonly bool _open;
    private readonly bool _canSell;
    private readonly List<RpMarket.MarketRow> _browse;
    private readonly List<RpMarket.MarketRow> _mine;
    private readonly string _notice;
    private readonly int _now;

    public uint MessageId => ServerPacketHeader.RpMarketComposer;

    public RpMarketComposer(bool open, bool canSell, List<RpMarket.MarketRow> browse, List<RpMarket.MarketRow> mine, string notice, int now)
    {
        _open = open;
        _canSell = canSell;
        _browse = browse ?? new();
        _mine = mine ?? new();
        _notice = notice ?? string.Empty;
        _now = now;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_open);
        packet.WriteBoolean(_canSell);
        packet.WriteInteger(RpMarket.MaxListings);
        packet.WriteInteger(RpMarket.MaxPrice);
        packet.WriteInteger(_browse.Count);
        foreach (var row in _browse)
        {
            packet.WriteInteger(row.Id);
            packet.WriteString(row.Item);
            packet.WriteInteger(row.Wear);
            packet.WriteInteger(RpDurability.MaxOf(row.Item));
            packet.WriteInteger(row.Price);
            packet.WriteInteger(row.LeftQty);
            packet.WriteInteger(row.ListedAt);
        }
        packet.WriteInteger(_mine.Count);
        foreach (var row in _mine)
        {
            packet.WriteInteger(row.Id);
            packet.WriteString(row.Item);
            packet.WriteInteger(row.Wear);
            packet.WriteInteger(RpDurability.MaxOf(row.Item));
            packet.WriteInteger(row.Price);
            packet.WriteInteger(row.ListedQty);
            packet.WriteInteger(row.LeftQty);
            packet.WriteInteger(row.SoldQty);
            packet.WriteInteger(row.ListedAt);
            packet.WriteInteger(row.EndedAt);
        }
        packet.WriteString(_notice);
        packet.WriteInteger(PoliceUtility.PoliceGear.Count);
        foreach (var item in PoliceUtility.PoliceGear)
            packet.WriteString(item);
        packet.WriteInteger(_now);
        packet.WriteInteger(RpMarket.ListingSeconds);
    }
}
