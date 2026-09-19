namespace Plus.HabboHotel.Catalog.Utilities;

/// <summary>Why a furniture type has no Buy link, so "not for sale" can be
/// told apart from "the shop is misconfigured" and from no answer at all.</summary>
public enum CatalogLocateStatus
{
    Found = 0,
    /// <summary>No catalog row anywhere sells this sprite.</summary>
    NotSold = 1,
    /// <summary>Rows exist, but every one of them sits on a shelf this player
    /// cannot reach. A shop problem, not an item problem.</summary>
    NotReachable = 2,
    /// <summary>The whole shop is staff-only, so there is nothing to link to.</summary>
    NotPermitted = 3
}

public readonly record struct CatalogLocation(CatalogItem? Item, CatalogLocateStatus Status);

public static class CatalogLookup
{
    /// <summary>Rank and VIP, the test CatalogIndexComposer applies to every
    /// node it writes. A page that fails it is not in the client's tree at
    /// all, so nothing can link to it.</summary>
    private static bool CanSee(CatalogPage page, int rank, int vipRank) =>
        !(page.MinimumRank > rank || page.MinimumVip > vipRank && rank == 1);

    /// <summary>The leaf test: exactly the pages GetCatalogPageEvent will
    /// serve. Anything stricter hides a link that would have worked.</summary>
    public static bool IsOpenable(CatalogPage page, int rank, int vipRank) =>
        page.Enabled && page.Visible && CanSee(page, rank, vipRank);

    public static Dictionary<int, CatalogPage> Index(ICollection<CatalogPage> catalog) =>
        catalog.ToDictionary(page => page.Id);

    /// <summary>ONE rule for "this page is on the shelf for this player", used
    /// by the search box and by the infostand's Buy link so the two can never
    /// disagree about the same item again.
    ///
    /// The leaf must be openable. Every ancestor need only pass CanSee - NOT
    /// Visible. An invisible parent is still written into the index, so its
    /// children remain reachable by a direct link even though nobody can
    /// browse to them; requiring ancestor visibility here was what hid Buy on
    /// items the search box was happily returning.</summary>
    public static bool IsShoppable(IReadOnlyDictionary<int, CatalogPage> pages, CatalogPage page, int rank, int vipRank)
    {
        if (!IsOpenable(page, rank, vipRank))
            return false;

        var visited = new HashSet<int> { page.Id };
        var current = page;
        while (current.ParentId != -1)
        {
            if (!pages.TryGetValue(current.ParentId, out var parent))
                return false;
            if (!visited.Add(parent.Id) || !CanSee(parent, rank, vipRank))
                return false;
            current = parent;
        }

        return true;
    }

    public static CatalogLocation FindFurniture(ICollection<CatalogPage> catalog, int spriteId, bool wall,
        bool isStaff, int rank, int vipRank)
    {
        if (!isStaff)
            return new CatalogLocation(null, CatalogLocateStatus.NotPermitted);

        var pages = Index(catalog);
        var productType = wall ? "i" : "s";
        var matches = new List<CatalogItem>();
        var sold = false;

        foreach (var page in pages.Values)
        {
            foreach (var item in page.Items.Values)
            {
                if (item?.Definition == null || item.Definition.SpriteId != spriteId ||
                    item.Definition.ProductType != productType)
                    continue;

                // Seen at all, even on a shelf out of reach: that is the
                // difference between "not for sale" and "shop misconfigured".
                sold = true;

                if (IsShoppable(pages, page, rank, vipRank))
                    matches.Add(item);
            }
        }

        var best = matches
            .OrderBy(item => item.IsLimited && item.LimitedEditionSells >= item.LimitedEditionStack)
            .ThenBy(item => item.Amount != 1)
            .ThenBy(item => item.PageId).ThenBy(item => item.Id).FirstOrDefault();

        if (best != null)
            return new CatalogLocation(best, CatalogLocateStatus.Found);

        return new CatalogLocation(null, sold ? CatalogLocateStatus.NotReachable : CatalogLocateStatus.NotSold);
    }

    // Real catalog item IDs take precedence; retain legacy offer links.
    public static int ResolveSelection(CatalogPage page, int itemId) => page.Items.ContainsKey(itemId)
        ? itemId : page.ItemOffers.TryGetValue(itemId, out var offer) ? offer.Id : -1;
}
