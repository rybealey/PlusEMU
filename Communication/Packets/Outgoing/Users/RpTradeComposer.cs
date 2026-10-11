using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Trading;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the trade window (RpTrades), from one side's point of view.
///
/// Open: the trade's id, who it is with, both offers as (item, count) pairs -
/// yours first - each side's Ready and Confirmed, whether each offer fits the
/// other backpack, and what to tell this player about their last press.
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

    public uint MessageId => ServerPacketHeader.RpTradeComposer;

    public RpTradeComposer(int tradeId, int partnerId, string partnerName, RpTrades.Side mine, RpTrades.Side theirs,
        bool mineFits, bool theirsFits, string notice)
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
    }
}
