namespace Plus.HabboHotel.Catalog.Utilities;

public static class CatalogLookup
{
    public static CatalogItem? FindFurniture(ICollection<CatalogPage> catalog, int spriteId, bool wall,
        bool isStaff, int rank, int vipRank)
    {
        if (!isStaff) return null;
        var pages = catalog.ToDictionary(page => page.Id);

        bool Accessible(CatalogPage page)
        {
            var visited = new HashSet<int>();
            while (true)
            {
                if (!visited.Add(page.Id) || !page.Visible || page.MinimumRank > rank ||
                    (page.MinimumVip > vipRank && rank == 1)) return false;
                if (page.ParentId == -1) return true;
                if (!pages.TryGetValue(page.ParentId, out page)) return false;
            }
        }

        return pages.Values.Where(page => page.Enabled && Accessible(page))
            .SelectMany(page => page.Items.Values)
            .Where(item => item.Definition.SpriteId == spriteId &&
                item.Definition.ProductType == (wall ? "i" : "s"))
            .OrderBy(item => item.IsLimited && item.LimitedEditionSells >= item.LimitedEditionStack)
            .ThenBy(item => item.Amount != 1)
            .ThenBy(item => item.PageId).ThenBy(item => item.Id).FirstOrDefault();
    }

    // Real catalog item IDs take precedence; retain legacy offer links.
    public static int ResolveSelection(CatalogPage page, int itemId) => page.Items.ContainsKey(itemId)
        ? itemId : page.ItemOffers.TryGetValue(itemId, out var offer) ? offer.Id : -1;
}
