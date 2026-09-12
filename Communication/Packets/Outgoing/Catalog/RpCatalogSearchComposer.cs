using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

/// <summary>
/// pixelrp: what the shop search found, item by item.
///
/// The stock search is client-side and cannot work here. It decides an item is
/// purchasable by looking its offer id up in a map the catalog index builds -
/// and every catalog row in this hotel carries offer_id -1, which
/// CatalogManager skips when filling that map. So the map is empty, every
/// furniture match is discarded for having no page behind it, and the only
/// results left are the category names FilterCatalogNode returns. Typing a
/// classname found nothing, because nothing could ever be found.
///
/// Searching server-side fixes the cause rather than the symptom: the catalog
/// is already in memory here, complete, with each item's real page beside it.
/// Nothing has to be inferred from an id that was never filled in.
///
/// Each result carries the page it is really sold on, which is what makes it
/// buyable - PurchaseFromCatalogEvent resolves an item inside a page, and the
/// client's purchase widget already prefers an offer's own page over the one
/// it is being displayed in.
/// </summary>
public class RpCatalogSearchComposer : IServerPacket
{
    private readonly string _query;
    private readonly IReadOnlyList<CatalogSearchHit> _hits;

    public uint MessageId => ServerPacketHeader.RpCatalogSearchComposer;

    public RpCatalogSearchComposer(string query, IReadOnlyList<CatalogSearchHit> hits)
    {
        _query = query;
        _hits = hits;
    }

    public void Compose(IOutgoingPacket packet)
    {
        // The query rides back so a slow search cannot overwrite a newer one:
        // the client drops a result whose query is not the one in the box.
        packet.WriteString(_query);
        packet.WriteInteger(_hits.Count);

        foreach (var hit in _hits)
        {
            packet.WriteInteger(hit.PageId);
            packet.WriteInteger(hit.ItemId);
            packet.WriteInteger(hit.FurnitureId);
            packet.WriteString(hit.ClassName);
            packet.WriteString(hit.Name);
            packet.WriteBoolean(hit.IsWallItem);
            packet.WriteInteger(hit.CostCredits);
            packet.WriteInteger(hit.CostPixels);
            packet.WriteInteger(hit.CostDiamonds);
        }
    }
}

/// <summary>One item the search matched, with everything the client needs to
/// draw it and everything the purchase needs to accept it.</summary>
public sealed record CatalogSearchHit(
    int PageId,
    int ItemId,
    int FurnitureId,
    string ClassName,
    string Name,
    bool IsWallItem,
    int CostCredits,
    int CostPixels,
    int CostDiamonds);
