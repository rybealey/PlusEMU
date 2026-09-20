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

    /// <summary>Can this player be sent to this page? The leaf must be
    /// openable, and every ancestor the walk can reach must pass CanSee.
    ///
    /// Ancestors are held to rank and VIP only, NOT Visible. An invisible
    /// parent is still written into the index along with its children, so a
    /// link to a child lands even though nobody can browse to it.
    ///
    /// A PARENT ROW THAT DOES NOT EXIST ENDS THE WALK RATHER THAN FAILING IT.
    /// Plenty of this catalog's stock sits under a parent_id pointing at a row
    /// that is gone, and refusing there hid the Buy button on furniture that
    /// sells perfectly well: there is no gate above a page whose parent does
    /// not exist, and the client loads a page the navigation tree never listed
    /// anyway. A CYCLE still fails - that is corrupt in a way this cannot
    /// reason about, and it is caught before any ancestor is skipped.
    ///
    /// This is a PERMISSION test, not a reachability one. Do not give it to
    /// the search box: a hit only has to be purchasable, and handing the box
    /// this rule emptied it. See RpCatalogSearchEvent.</summary>
    public static bool IsShoppable(IReadOnlyDictionary<int, CatalogPage> pages, CatalogPage page, int rank, int vipRank)
    {
        if (!IsOpenable(page, rank, vipRank))
            return false;

        var visited = new HashSet<int> { page.Id };
        var current = page;
        while (current.ParentId != -1)
        {
            if (!pages.TryGetValue(current.ParentId, out var parent))
                return true;
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
