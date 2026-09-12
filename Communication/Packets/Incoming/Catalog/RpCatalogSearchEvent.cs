using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Catalog;

/// <summary>
/// pixelrp: search the shop for an item by name or classname.
///
/// The search the client ships cannot work in this hotel - see
/// RpCatalogSearchComposer for why - so it runs here, over the catalog the
/// emulator already holds in memory. That is also the only place the answer is
/// knowable: whether a piece is for sale is a fact about catalog_items, and the
/// client is never sent more than the page it is looking at.
///
/// VISIBILITY IS THE SAME TEST THE TREE USES. A page the player cannot see in
/// the navigator must not leak its stock through the search box, so this
/// mirrors CatalogIndexComposer.CanSee exactly - rank, VIP, enabled, visible -
/// rather than inventing a second rule that can drift from it. Purchase
/// re-checks all of it again anyway.
/// </summary>
internal class RpCatalogSearchEvent : IPacketEvent
{
    /// <summary>Two characters is where a query stops being every item in the
    /// hotel. Below it the client gets an empty result rather than a refusal:
    /// they are still typing.</summary>
    private const int MinimumQuery = 2;

    /// <summary>Enough to be worth scrolling, few enough that the packet stays
    /// small and the grid stays usable. A query that hits the cap is a query
    /// that wants narrowing.</summary>
    private const int MaxHits = 150;

    private readonly ICatalogManager _catalogManager;

    public RpCatalogSearchEvent(ICatalogManager catalogManager)
    {
        _catalogManager = catalogManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        var raw = packet.ReadString() ?? string.Empty;

        if (habbo == null)
            return Task.CompletedTask;

        // Spaces out of both sides of the comparison, so "dark hardwood" finds
        // "Dark Hardwood Floor" and "darkhardwood" does too.
        var query = new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

        if (query.Length < MinimumQuery)
        {
            session.Send(new RpCatalogSearchComposer(raw, Array.Empty<CatalogSearchHit>()));
            return Task.CompletedTask;
        }

        var hits = new List<CatalogSearchHit>();

        foreach (var page in _catalogManager.Pages)
        {
            if (!page.Enabled || !page.Visible)
                continue;
            if (page.MinimumRank > habbo.Rank || (page.MinimumVip > habbo.VipRank && habbo.Rank == 1))
                continue;

            foreach (var item in page.Items.Values)
            {
                if (item?.Definition == null)
                    continue;

                // Floor and wall only. The client builds each result from its
                // FurnitureData entry, and a bot, effect, badge or pet has
                // none - the same split the icon audit draws.
                if (item.Definition.ProductType != "s" && item.Definition.ProductType != "i")
                    continue;

                var className = item.Definition.ItemName ?? string.Empty;
                var name = item.CatalogName ?? string.Empty;

                if (!Matches(className, query) && !Matches(name, query))
                    continue;

                hits.Add(new CatalogSearchHit(
                    page.Id,
                    item.Id,
                    (int)item.Definition.Id,
                    className,
                    name,
                    item.Definition.ProductType == "i",
                    item.CostCredits,
                    item.CostPixels,
                    item.CostDiamonds));

                if (hits.Count >= MaxHits)
                    break;
            }

            if (hits.Count >= MaxHits)
                break;
        }

        session.Send(new RpCatalogSearchComposer(raw, hits));
        return Task.CompletedTask;
    }

    private static bool Matches(string haystack, string query) =>
        new string(haystack.Where(c => !char.IsWhiteSpace(c)).ToArray())
            .ToLowerInvariant().Contains(query);
}
