using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Plus.Database;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Navigator.SavedSearches;

namespace Plus.HabboHotel.Navigator;

public sealed class NavigatorManager : INavigatorManager
{
    private readonly IDatabase _database;
    private readonly ILogger<NavigatorManager> _logger;

    private readonly Dictionary<uint, FeaturedRoom> _featuredRooms;
    private readonly Dictionary<int, SearchResultList> _searchResultLists;
    private readonly Dictionary<int, TopLevelItem> _topLevelItems;

    public NavigatorManager(IDatabase database, ILogger<NavigatorManager> logger)
    {
        _database = database;
        _logger = logger;
        _topLevelItems = new();
        _searchResultLists = new();

        //Does this need to be dynamic?
        _topLevelItems.Add(1, new(1, "official_view", "", ""));
        _topLevelItems.Add(2, new(2, "hotel_view", "", ""));
        // pixelrp: no Events tab (roomads_view, the promoted-rooms list)
        _topLevelItems.Add(4, new(4, "myworld_view", "", ""));
        _featuredRooms = new();
    }

    public void Init()
    {
        if (_searchResultLists.Count > 0)
            _searchResultLists.Clear();
        if (_featuredRooms.Count > 0)
            _featuredRooms.Clear();
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT * FROM `navigator_categories` ORDER BY `id` ASC");
            var table = dbClient.GetTable();
            if (table != null)
            {
                foreach (DataRow row in table.Rows)
                {
                    if (Convert.ToInt32(row["enabled"]) == 1)
                    {
                        if (!_searchResultLists.ContainsKey(Convert.ToInt32(row["id"])))
                        {
                            _searchResultLists.Add(Convert.ToInt32(row["id"]),
                                new(Convert.ToInt32(row["id"]), Convert.ToString(row["category"]), Convert.ToString(row["category_identifier"]), Convert.ToString(row["public_name"]),
                                    true, -1, Convert.ToInt32(row["required_rank"]), NavigatorViewModeUtility.GetViewModeByString(Convert.ToString(row["view_mode"])),
                                    Convert.ToString(row["category_type"]), Convert.ToString(row["search_allowance"]), Convert.ToInt32(row["order_id"])));
                        }
                    }
                }
            }
            dbClient.SetQuery("SELECT `room_id`,`caption`,`description`,`image_url`,`enabled` FROM `navigator_publics` ORDER BY `order_num` ASC");
            var getPublics = dbClient.GetTable();
            if (getPublics != null)
            {
                foreach (DataRow row in getPublics.Rows)
                {
                    if (Convert.ToInt32(row["enabled"]) == 1)
                    {
                        if (!_featuredRooms.ContainsKey(Convert.ToUInt32(row["room_id"])))
                        {
                            _featuredRooms.Add(Convert.ToUInt32(row["room_id"]),
                                new(Convert.ToInt32(row["room_id"]), Convert.ToString(row["caption"]), Convert.ToString(row["description"]), Convert.ToString(row["image_url"])));
                        }
                    }
                }
            }
        }
        _logger.LogInformation("Navigator -> LOADED");
    }

    public List<SearchResultList> GetCategoriessForSearch(string category) => _searchResultLists.Where(cat => cat.Value.Category == category).OrderBy(cat => cat.Value.OrderId).Select(cat => cat.Value).ToList();

    public IReadOnlyCollection<SearchResultList> GetResultByIdentifier(string category) => _searchResultLists.Where(cat => cat.Value.CategoryIdentifier == category).OrderBy(cat => cat.Value.OrderId).Select(cat => cat.Value).ToList();

    /// <summary>pixelrp: the start of a Public tab list's identifier - "pixelrp_corporations".</summary>
    public const string PixelRpListPrefix = "pixelrp_";

    /// <summary>
    /// pixelrp: the Public tab. The PixelRP rooms - the city's own, where the
    /// gameplay is held - one list per room category: Corporations first, then
    /// the All Rooms order. A category holding no PixelRP room is left out, so
    /// the tab shows no empty headers. Given an identifier, just that list -
    /// its "show more" - whether or not it holds a room yet.
    ///
    /// Built here from the room categories rather than stored: a new room
    /// category shows up on the Public tab with nothing else to change.
    /// </summary>
    public List<SearchResultList> GetPixelRpPublicLists(string identifier = null)
    {
        var accountId = PixelRpRooms.AccountId;
        if (accountId <= 0)
            return new();
        var lists = FlatCategories
            .OrderBy(cat => cat.Id == RoomCategories.CorporationsId ? 0 : 1)
            .ThenBy(cat => cat.OrderId)
            .Select((cat, index) => new SearchResultList(cat.Id, "official_view", PixelRpListPrefix + cat.CategoryIdentifier, cat.PublicName, true, -1,
                cat.RequiredRank, cat.ViewMode, "pixelrp_category", "SHOW_MORE", index))
            .ToList();
        if (identifier != null)
            return lists.Where(list => list.CategoryIdentifier == identifier).ToList();
        var filled = new HashSet<int>();
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT DISTINCT `category` FROM `rooms` WHERE `owner` = @owner AND `state` != 'invisible'");
            dbClient.AddParameter("owner", accountId);
            var table = dbClient.GetTable();
            if (table != null)
            {
                foreach (DataRow row in table.Rows)
                {
                    if (int.TryParse(Convert.ToString(row["category"]), out var category))
                        filled.Add(category);
                }
            }
        }
        return lists.Where(list => filled.Contains(list.Id)).ToList();
    }

    public IReadOnlyCollection<SearchResultList> FlatCategories => _searchResultLists.Where(cat => cat.Value.CategoryType == NavigatorCategoryType.Category).OrderBy(cat => cat.Value.OrderId).Select(cat => cat.Value).ToList();

    public IReadOnlyCollection<SearchResultList> EventCategories => _searchResultLists.Where(cat => cat.Value.CategoryType == NavigatorCategoryType.PromotionCategory).OrderBy(cat => cat.Value.OrderId).Select(cat => cat.Value).ToList();

    public IReadOnlyCollection<TopLevelItem> TopLevelItems => _topLevelItems.Values;

    public IReadOnlyCollection<SearchResultList> SearchResultLists => _searchResultLists.Values;

    public bool TryGetTopLevelItem(int id, out TopLevelItem topLevelItem) => _topLevelItems.TryGetValue(id, out topLevelItem);

    public bool TryGetSearchResultList(int id, out SearchResultList searchResultList) => _searchResultLists.TryGetValue(id, out searchResultList);

    public bool TryGetFeaturedRoom(uint roomId, out FeaturedRoom publicRoom) => _featuredRooms.TryGetValue(roomId, out publicRoom);

    public IReadOnlyCollection<FeaturedRoom> FeaturedRooms => _featuredRooms.Values;

    public async Task<Dictionary<int, SavedSearch>> LoadUserNavigatorPreferences(int userId)
    {
        using var connection = _database.Connection();
        return (await connection.QueryAsync<SavedSearch>("SELECT `id`,`filter`,`search_code` as search FROM `user_saved_searches` WHERE `user_id` = @userId", new { userId })).ToDictionary(search => search.Id);
    }

    public async Task SaveHomeRoom(Habbo habbo, uint roomId)
    {
        habbo.HomeRoom = roomId;

        if (!RoomFactory.TryGetData(roomId, out _))
            return;

        using var connection = _database.Connection();
        await connection.ExecuteAsync("UPDATE users SET home_room = @roomid WHERE id = @userid LIMIT 1", new { roomid = roomId, userid = habbo.Id });
    }
}