namespace Plus.HabboHotel.Navigator;

public static class RoomCategories
{
    /// <summary>
    ///     Where a room lands when the category it was given cannot be honoured — unknown id, not a
    ///     room category at all, or above the sender's rank. Must stay an enabled `category` row in
    ///     `navigator_categories` with a required rank of 1, or the room ends up filed under nothing
    ///     and drops out of the navigator entirely.
    /// </summary>
    public const int FallbackId = 30; // Residential
}
