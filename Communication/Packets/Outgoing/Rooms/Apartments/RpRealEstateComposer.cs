using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Apartments;

/// <summary>
/// pixelrp: the real estate board as one player sees it (RpRealEstate.Send) -
/// sent with `open` set by the step onto a Real Estate point, and after every
/// action without it (a closed board ignores those).
///
/// Whether they own an apartment already (one each: they cannot buy), the lot
/// grid (lots along a side, tiles along a lot), then every listing: its room
/// id, owner, room name, building, asking price, whether there is furni in
/// it, whether it is theirs (or their own account's), and the lots it covers.
/// Last, the note on their last action.
/// </summary>
public class RpRealEstateComposer : IServerPacket
{
    private readonly bool _open;
    private readonly bool _ownsOne;
    private readonly List<RpRealEstate.ListingRow> _listings;
    private readonly string _notice;

    public uint MessageId => ServerPacketHeader.RpRealEstateComposer;

    public RpRealEstateComposer(bool open, bool ownsOne, List<RpRealEstate.ListingRow> listings, string notice)
    {
        _open = open;
        _ownsOne = ownsOne;
        _listings = listings ?? new();
        _notice = notice ?? string.Empty;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_open);
        packet.WriteBoolean(_ownsOne);
        packet.WriteInteger(RpApartments.Grid);
        packet.WriteInteger(RpApartments.LotSize);
        packet.WriteInteger(_listings.Count);
        foreach (var listing in _listings)
        {
            var lots = RpApartments.LotsOf(new RpApartments.ApartmentRow { Lots = listing.Lots });
            packet.WriteInteger((int)listing.RoomId);
            packet.WriteString(listing.OwnerName);
            packet.WriteString(listing.RoomName);
            packet.WriteString(listing.Building);
            packet.WriteInteger(listing.ListPrice);
            packet.WriteBoolean(listing.FurniCount > 0);
            packet.WriteBoolean(listing.Mine);
            packet.WriteInteger(lots.Count);
            foreach (var lot in lots)
                packet.WriteInteger(lot);
        }
        packet.WriteString(_notice);
    }
}
