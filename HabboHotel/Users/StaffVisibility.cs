using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Users;

/// <summary>
/// pixelrp: whether a viewer may see staff who have marked themselves hidden.
///
/// `users.hidden_staff` is a CMS flag - it takes a staff member off the
/// website's team page - and the rank that overrides it is a CMS setting,
/// `min_rank_to_see_hidden_staff` (6 out of the box). Neither had any effect
/// in the hotel, so somebody hidden on the website was still listed in their
/// corporation's roster, which is the same information by another door.
///
/// Read fresh on use rather than cached: it is one indexed lookup on a
/// settings table, and it runs when somebody opens a corporation, not in a
/// loop.
/// </summary>
public static class StaffVisibility
{
    /// <summary>What the setting falls back to when it is missing or unreadable.</summary>
    private const int DefaultMinRank = 6;

    /// <summary>
    /// True when this session outranks the hidden flag. Everyone else sees a
    /// roster with that person simply absent - not greyed, not counted.
    /// </summary>
    public static bool CanSeeHiddenStaff(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return false;
        return habbo.Rank >= MinRank();
    }

    private static int MinRank()
    {
        try
        {
            using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
            dbClient.SetQuery("SELECT `value` FROM `website_settings` WHERE `key` = 'min_rank_to_see_hidden_staff' LIMIT 1");
            var row = dbClient.GetRow();
            if (row == null)
                return DefaultMinRank;
            return int.TryParse(Convert.ToString(row["value"]), out var rank) ? rank : DefaultMinRank;
        }
        catch
        {
            // A missing settings table must not make hidden staff visible -
            // failing closed is the only safe direction for this one.
            return DefaultMinRank;
        }
    }
}
