using System.Data;
using Plus.Database;

namespace Plus.HabboHotel.Users.Messenger;

public class SearchResultFactory : ISearchResultFactory
{
    private readonly IDatabase _database;

    public SearchResultFactory(IDatabase database)
    {
        _database = database;
    }

    public List<SearchResult> GetSearchResult(string query)
    {
        DataTable dTable;
        using (var dbClient = _database.GetQueryReactor())
        {
            // pixelrp: the hotel's own accounts (PixelRP) are never found.
            dbClient.SetQuery("SELECT `id`,`username`,`motto`,`look`,`last_online` FROM users WHERE username LIKE @query AND `rp_system_account` = 0 LIMIT 50");
            dbClient.AddParameter("query", $"{query}%");
            dTable = dbClient.GetTable();
        }
        var results = new List<SearchResult>();
        if (dTable != null)
        {
            foreach (DataRow dRow in dTable.Rows)
                results.Add(new(Convert.ToInt32(dRow[0]), Convert.ToString(dRow[1]), Convert.ToString(dRow[2]), Convert.ToString(dRow[3]), dRow[4].ToString()));
        }
        return results;
    }
}