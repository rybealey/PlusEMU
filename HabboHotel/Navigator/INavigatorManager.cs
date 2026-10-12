using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Navigator.SavedSearches;

namespace Plus.HabboHotel.Navigator;

public interface INavigatorManager
{
    void Init();
    List<SearchResultList> GetCategoriessForSearch(string category);
    IReadOnlyCollection<SearchResultList> GetResultByIdentifier(string category);
    List<SearchResultList> GetPixelRpPublicLists(string identifier = null);
    IReadOnlyCollection<SearchResultList> FlatCategories { get; }
    IReadOnlyCollection<SearchResultList> EventCategories { get; }
    IReadOnlyCollection<TopLevelItem> TopLevelItems { get; }
    IReadOnlyCollection<SearchResultList> SearchResultLists { get; }
    bool TryGetTopLevelItem(int id, out TopLevelItem topLevelItem);
    bool TryGetSearchResultList(int id, out SearchResultList searchResultList);
    bool TryGetFeaturedRoom(uint roomId, out FeaturedRoom publicRoom);
    IReadOnlyCollection<FeaturedRoom> FeaturedRooms { get; }
    Task<Dictionary<int, SavedSearch>> LoadUserNavigatorPreferences(int habboId);
    Task SaveHomeRoom(Habbo habbo, uint roomId);
}