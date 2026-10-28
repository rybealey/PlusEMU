using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Apartments;

/// <summary>
/// pixelrp: the Apartments window as one player sees it (RpApartments.Send) -
/// sent with `open` set by the step onto an Apartments point, and after every
/// action without it (a closed window ignores those).
///
/// The prices (the apartment, the second lot and how much dearer each lot
/// after it is, the sell-back percentage, the most an asking price may be,
/// the listing fee),
/// the lot grid (lots along a side, tiles along a lot, the centre lot) and the
/// building's name. Then their apartment: whether they have one, its room id
/// and name, when it was bought, everything paid for it, its asking price on
/// the real estate board (0 when not for sale) and the lots it covers. Last,
/// the note on their last action.
/// </summary>
public class RpApartmentComposer : IServerPacket
{
    private readonly bool _open;
    private readonly string _building;
    private readonly RpApartments.ApartmentRow? _apartment;
    private readonly List<int> _lots;
    private readonly string _notice;

    public uint MessageId => ServerPacketHeader.RpApartmentComposer;

    public RpApartmentComposer(bool open, string building, RpApartments.ApartmentRow? apartment, List<int> lots, string notice)
    {
        _open = open;
        _building = building ?? string.Empty;
        _apartment = apartment;
        _lots = lots ?? new();
        _notice = notice ?? string.Empty;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_open);
        packet.WriteInteger(RpApartments.Price);
        packet.WriteInteger(RpApartments.LotPriceBase);
        packet.WriteInteger(RpApartments.LotPriceStep);
        packet.WriteInteger(RpApartments.SellBackPercent);
        packet.WriteInteger(RpApartments.MaxListPrice);
        packet.WriteInteger(RpApartments.ListingFee);
        packet.WriteInteger(RpApartments.Grid);
        packet.WriteInteger(RpApartments.LotSize);
        packet.WriteInteger(RpApartments.CentreLot);
        packet.WriteString(_building);
        packet.WriteBoolean(_apartment != null);
        packet.WriteInteger((int)(_apartment?.RoomId ?? 0));
        packet.WriteString(_apartment?.RoomName ?? string.Empty);
        packet.WriteInteger(_apartment?.BoughtAt ?? 0);
        packet.WriteInteger(_apartment?.Paid ?? 0);
        packet.WriteInteger(_apartment?.ListPrice ?? 0);
        packet.WriteInteger(_lots.Count);
        foreach (var lot in _lots)
            packet.WriteInteger(lot);
        packet.WriteString(_notice);
    }
}
