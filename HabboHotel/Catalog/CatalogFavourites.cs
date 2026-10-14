using Plus.Database;

namespace Plus.HabboHotel.Catalog;

/// <summary>
/// pixelrp: the shop categories a player has starred (256_CatalogFavourites).
///
/// Kept per ACCOUNT, not per character - the caller passes the account root
/// (AccountUtility.RootOf) - so every character of one person shares a list.
/// A page that has since been deleted or hidden simply stays in the table; the
/// client shows only the favourites it can find in its own shop tree.
/// </summary>
public static class CatalogFavourites
{
    /// Plenty for a quick-access list, and a ceiling on what one account can
    /// write.
    public const int Maximum = 50;

    public static List<int> Load(IDatabase database, int accountId)
    {
        var pageIds = new List<int>();
        using var dbClient = database.GetQueryReactor();
        dbClient.SetQuery("SELECT `page_id` FROM `rp_catalog_favourites` WHERE `account_id` = @accountId ORDER BY `created_at`, `page_id`");
        dbClient.AddParameter("accountId", accountId);
        var table = dbClient.GetTable();
        if (table == null)
            return pageIds;
        foreach (System.Data.DataRow row in table.Rows)
            pageIds.Add(Convert.ToInt32(row["page_id"]));
        return pageIds;
    }

    public static void Set(IDatabase database, int accountId, int pageId, bool favourite)
    {
        using var dbClient = database.GetQueryReactor();
        dbClient.SetQuery(favourite
            ? "INSERT IGNORE INTO `rp_catalog_favourites` (`account_id`, `page_id`) VALUES (@accountId, @pageId)"
            : "DELETE FROM `rp_catalog_favourites` WHERE `account_id` = @accountId AND `page_id` = @pageId");
        dbClient.AddParameter("accountId", accountId);
        dbClient.AddParameter("pageId", pageId);
        dbClient.RunQuery();
    }
}
