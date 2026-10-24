using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Trading;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the trade window (RpTrades), from one side's point of view.
///
/// Open: the trade's id, who it is with, both offers as (item, count) pairs -
/// yours first - each side's Ready and Confirmed, whether each offer fits the
/// other backpack, and what to tell this player about their last press - then
/// how worn each weapon and suit of armour on offer is, for its bar.
/// Closed: how it ended (RpTrades.Result*) and a line saying so.
/// </summary>
public class RpTradeComposer : IServerPacket
{
    private readonly bool _open;
    private readonly int _tradeId;
    private readonly int _partnerId;
    private readonly string _partnerName = "";
    private readonly RpTrades.Side? _mine;
    private readonly RpTrades.Side? _theirs;
    private readonly bool _mineFits;
    private readonly bool _theirsFits;
    private readonly string _notice = "";
    private readonly int _result;
    private readonly List<(string Item, int Wear, int Max)> _mineWorn = new();
    private readonly List<(string Item, int Wear, int Max)> _theirsWorn = new();

    public uint MessageId => ServerPacketHeader.RpTradeComposer;

    public RpTradeComposer(int tradeId, int partnerId, string partnerName, RpTrades.Side mine, RpTrades.Side theirs,
        bool mineFits, bool theirsFits, string notice,
        List<(string Item, int Wear, int Max)>? mineWorn = null, List<(string Item, int Wear, int Max)>? theirsWorn = null)
    {
        _open = true;
        _tradeId = tradeId;
        _partnerId = partnerId;
        _partnerName = partnerName;
        _mine = mine;
        _theirs = theirs;
        _mineFits = mineFits;
        _theirsFits = theirsFits;
        _notice = notice ?? "";
        _mineWorn = mineWorn ?? new();
        _theirsWorn = theirsWorn ?? new();
    }

    private RpTradeComposer(int result, string message)
    {
        _open = false;
        _result = result;
        _notice = message ?? "";
    }

    public static RpTradeComposer Closed(int result, string message) => new(result, message);

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_open);
        if (!_open)
        {
            packet.WriteInteger(_result);
            packet.WriteString(_notice);
            return;
        }
        packet.WriteInteger(_tradeId);
        packet.WriteInteger(_partnerId);
        packet.WriteString(_partnerName);
        foreach (var side in new[] { _mine!, _theirs! })
        {
            var offer = side.Offer.ToList();
            packet.WriteInteger(offer.Count);
            foreach (var (item, count) in offer)
            {
                packet.WriteString(item);
                packet.WriteInteger(count);
            }
            packet.WriteBoolean(side.Ready);
            packet.WriteBoolean(side.Confirmed);
        }
        packet.WriteBoolean(_mineFits);
        packet.WriteBoolean(_theirsFits);
        packet.WriteString(_notice);
        // pixelrp durability: how worn each weapon or suit of armour on offer
        // is, yours then theirs, in the order the swap hands them over - for
        // the bar on it (RpTrades.OfferWear).
        foreach (var worn in new[] { _mineWorn, _theirsWorn })
        {
            packet.WriteInteger(worn.Count);
            foreach (var (item, wear, max) in worn)
            {
                packet.WriteString(item);
                packet.WriteInteger(wear);
                packet.WriteInteger(max);
            }
        }
    }
}
