using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Database;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Catalog.Utilities;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// pixelrp: move one shop listing to another category, from the shop itself.
///
/// The same proof as a shop rename (RpCatalogRenameFurniEvent): the listing has
/// to be on a catalog page, and it is found there rather than trusted from the
/// packet. It moves ONE `catalog_items` row - a furni sold in two categories
/// keeps its other listing.
///
/// The row is moved in the database and in the loaded catalog together, so
/// the next page request, search or restart all agree. The two pages' item
/// maps are REPLACED, never edited in place: other connections enumerate them
/// while composing pages, and a map that changes under an enumeration throws.
///
/// A moved listing slots in by its id, which is the order the shop lists rows
/// in on every load - so where it shows now is where it shows after a restart.
/// Every move is logged (254_CatalogMoveLog).
/// </summary>
internal class RpCatalogMoveFurniEvent : IPacketEvent
{
    // The layouts that are a plain grid of furni. Every other one (bots, pets,
    // spaces, trophies, badge display...) builds its page from its own kind of
    // row, so a furni dropped in would break it or never show - and a furni
    // taken out of one would leave that page short of what it is for.
    private static readonly HashSet<string> FurniLayouts = new() { "default_3x3", "default_3x3_color_grouping" };

    private readonly IDatabase _database;
    private readonly ICatalogManager _catalogManager;

    public RpCatalogMoveFurniEvent(IDatabase database, ICatalogManager catalogManager)
    {
        _database = database;
        _catalogManager = catalogManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var catalogItemId = packet.ReadInt();
        var targetPageId = packet.ReadInt();

        var habbo = session.GetHabbo();
        if (habbo == null || !habbo.CanEditShop)
            return Task.CompletedTask;

        CatalogPage source = null;
        CatalogItem item = null;
        foreach (var page in _catalogManager.Pages)
        {
            if (page?.Items == null || !page.Items.TryGetValue(catalogItemId, out var found))
                continue;
            source = page;
            item = found;
            break;
        }

        void Reply(bool moved, string message) =>
            session.Send(new RpCatalogMoveFurniComposer(moved, catalogItemId, targetPageId, message));

        if (item == null || source == null)
        {
            Reply(false, "That furni is no longer in the shop.");
            return Task.CompletedTask;
        }
        if (!FurniLayouts.Contains(source.Layout ?? string.Empty))
        {
            Reply(false, "Furni on this page cannot be moved.");
            return Task.CompletedTask;
        }
        if (!_catalogManager.TryGetPage(targetPageId, out var target) || target == null)
        {
            Reply(false, "That category no longer exists.");
            return Task.CompletedTask;
        }
        // Headings and dividers are disabled pages, and a mirror page holds
        // nothing of its own - it shows another page's branch.
        if (!target.Enabled || target.Link == "mirror" || !FurniLayouts.Contains(target.Layout ?? string.Empty))
        {
            Reply(false, "That category cannot hold furni.");
            return Task.CompletedTask;
        }
        if (target.Id == source.Id)
        {
            Reply(false, "It is already in that category.");
            return Task.CompletedTask;
        }
        // Below the staff line, only between shelves that rank can buy from.
        // Otherwise a Builder could carry staff stock (Staff > Wired, Bots...)
        // out onto their own shelves and buy it, or bury a listing where
        // nobody below staff can reach it.
        if (!habbo.IsStaff)
        {
            var index = CatalogLookup.Index(_catalogManager.Pages);
            if (!CatalogLookup.IsBuyable(index, source, habbo.Rank, habbo.VipRank))
            {
                Reply(false, "You can only move furni from categories you can shop in.");
                return Task.CompletedTask;
            }
            if (!CatalogLookup.IsBuyable(index, target, habbo.Rank, habbo.VipRank))
            {
                Reply(false, "You can only move furni to categories you can shop in.");
                return Task.CompletedTask;
            }
        }

        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `catalog_items` SET `page_id` = @pageId WHERE `id` = @catalogItemId LIMIT 1");
            dbClient.AddParameter("pageId", target.Id);
            dbClient.AddParameter("catalogItemId", item.Id);
            dbClient.RunQuery();

            dbClient.SetQuery("INSERT INTO `rp_catalog_move_log` (`catalog_item_id`, `definition_id`, `item_name`, `from_page_id`, " +
                              "`from_caption`, `to_page_id`, `to_caption`, `user_id`, `username`) VALUES (@catalogItemId, " +
                              "@definitionId, @itemName, @fromPageId, @fromCaption, @toPageId, @toCaption, @userId, @username)");
            dbClient.AddParameter("catalogItemId", item.Id);
            dbClient.AddParameter("definitionId", item.Definition?.Id ?? item.ItemId);
            dbClient.AddParameter("itemName", item.Definition?.ItemName ?? string.Empty);
            dbClient.AddParameter("fromPageId", source.Id);
            dbClient.AddParameter("fromCaption", source.Caption ?? string.Empty);
            dbClient.AddParameter("toPageId", target.Id);
            dbClient.AddParameter("toCaption", target.Caption ?? string.Empty);
            dbClient.AddParameter("userId", habbo.Id);
            dbClient.AddParameter("username", habbo.Username ?? string.Empty);
            dbClient.RunQuery();
        }

        item.PageId = target.Id;
        source.Items = source.Items.Values.Where(x => x.Id != item.Id).ToDictionary(x => x.Id);
        target.Items = target.Items.Values.Append(item).OrderBy(x => x.Id).ToDictionary(x => x.Id);
        source.ItemOffers = OffersOf(source.Items);
        target.ItemOffers = OffersOf(target.Items);

        // Which page sells an offer, for the purchase and club-offer lookups -
        // moved along with it unless the old page still sells that offer.
        if (item.OfferId != -1 && _catalogManager.ItemOffers.TryGetValue(item.OfferId, out var offerPageId) &&
            offerPageId == source.Id && !source.ItemOffers.ContainsKey(item.OfferId))
            _catalogManager.ItemOffers[item.OfferId] = target.Id;

        Reply(true, $"Moved to {target.Caption}.");
        return Task.CompletedTask;
    }

    /// The same rule CatalogManager.Init uses: the first listing on the page
    /// carrying each offer id.
    private static Dictionary<int, CatalogItem> OffersOf(Dictionary<int, CatalogItem> items)
    {
        var offers = new Dictionary<int, CatalogItem>();
        foreach (var listing in items.Values)
        {
            if (listing.OfferId != -1 && !offers.ContainsKey(listing.OfferId))
                offers.Add(listing.OfferId, listing);
        }
        return offers;
    }
}
