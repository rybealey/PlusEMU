namespace Plus.HabboHotel.Navigator;

public enum NavigatorCategoryType
{
    Category,
    Featured,
    Popular,
    Recommended,
    Query,
    MyRooms,
    MyFavourites,
    MyGroups,
    MyHistory,
    MyFriendsRooms,
    MyFrequentHistory,
    TopPromotions,
    PromotionCategory,
    MyRights,

    // pixelrp: the Public tab - the PixelRP rooms in one room category (the
    // list's id is that category's id). Built in code, not read from
    // `navigator_categories`: NavigatorManager.GetPixelRpPublicLists.
    PixelRpCategory
}