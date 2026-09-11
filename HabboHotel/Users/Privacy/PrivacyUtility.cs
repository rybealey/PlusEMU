using Plus.Database;

namespace Plus.HabboHotel.Users.Privacy;

/// <summary>
/// pixelrp: who may see the personal details on a player's profile.
///
/// Two fields, each with its own audience, because they leak different things.
/// A birthday is a real date about a real person; a region says roughly when
/// somebody is about, which is useful to the people they play with and nobody
/// else's business.
///
/// The rule is enforced where the data is READ, never where it is written -
/// a charge that a client asks for and does not get is the only safe shape,
/// since the profile window, the calendar and the room broadcast all reach
/// different people.
///
/// "Contacts" is the phone's Contacts app, which is the friends list; there is
/// no second address book. "Colleagues" is anyone employed by the same
/// corporation, on duty or not - employment is the relationship, not the shift.
/// </summary>
public static class PrivacyUtility
{
    // Birthday audiences, and the two ends of the region's.
    public const int Nobody = 0;
    public const int Contacts = 1;
    public const int Everyone = 2;

    /// <summary>Region only: the audience is whichever lists are switched on.</summary>
    public const int Custom = 1;

    /// <summary>
    /// What a player who has never opened the screen gets. Deliberately what
    /// the hotel did BEFORE the screen existed - region on the profile for
    /// anyone, birthday on the calendar for friends - so shipping this hides
    /// nothing somebody was already sharing.
    /// </summary>
    public static readonly ProfilePrivacy Default = new(Contacts, Everyone, true, true);

    public record ProfilePrivacy(int Birthday, int Region, bool RegionColleagues, bool RegionFriends);

    public static ProfilePrivacy Get(int userId)
    {
        if (userId <= 0)
            return Default;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT `birthday_visibility`, `region_visibility`, " +
            "(`region_colleagues` = 1) AS colleagues, (`region_friends` = 1) AS friends " +
            "FROM `rp_user_privacy` WHERE `user_id` = @id LIMIT 1");
        dbClient.AddParameter("id", userId);
        var row = dbClient.GetRow();
        if (row == null)
            return Default;
        return new ProfilePrivacy(
            Convert.ToInt32(row["birthday_visibility"]),
            Convert.ToInt32(row["region_visibility"]),
            Convert.ToBoolean(row["colleagues"]),
            Convert.ToBoolean(row["friends"]));
    }

    public static void Save(int userId, ProfilePrivacy privacy)
    {
        if (userId <= 0)
            return;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "INSERT INTO `rp_user_privacy` (`user_id`,`birthday_visibility`,`region_visibility`,`region_colleagues`,`region_friends`) " +
            "VALUES (@id,@birthday,@region,@colleagues,@friends) " +
            "ON DUPLICATE KEY UPDATE `birthday_visibility` = @birthday, `region_visibility` = @region, " +
            "`region_colleagues` = @colleagues, `region_friends` = @friends");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("birthday", Clamp(privacy.Birthday));
        dbClient.AddParameter("region", Clamp(privacy.Region));
        dbClient.AddParameter("colleagues", privacy.RegionColleagues ? 1 : 0);
        dbClient.AddParameter("friends", privacy.RegionFriends ? 1 : 0);
        dbClient.RunQuery();
    }

    /// <summary>Anything outside the three the screen offers is read as Nobody.</summary>
    private static int Clamp(int visibility) => (visibility < Nobody || visibility > Everyone) ? Nobody : visibility;

    public static bool CanSeeBirthday(int viewerId, int ownerId)
    {
        if (viewerId == ownerId)
            return true;
        return Get(ownerId).Birthday switch
        {
            Everyone => true,
            Contacts => AreFriends(viewerId, ownerId),
            _ => false
        };
    }

    public static bool CanSeeRegion(int viewerId, int ownerId)
    {
        if (viewerId == ownerId)
            return true;
        var privacy = Get(ownerId);
        return privacy.Region switch
        {
            Everyone => true,
            // Both switches off is the same as sharing with no one, which is
            // what the screen's own footnote promises.
            Custom => (privacy.RegionColleagues && AreColleagues(viewerId, ownerId))
                      || (privacy.RegionFriends && AreFriends(viewerId, ownerId)),
            _ => false
        };
    }

    /// <summary>A friendship is one row either way round.</summary>
    public static bool AreFriends(int a, int b)
    {
        if (a <= 0 || b <= 0)
            return false;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT 1 FROM `messenger_friendships` " +
            "WHERE (`user_one_id` = @a AND `user_two_id` = @b) OR (`user_one_id` = @b AND `user_two_id` = @a) LIMIT 1");
        dbClient.AddParameter("a", a);
        dbClient.AddParameter("b", b);
        return dbClient.GetRow() != null;
    }

    /// <summary>Employed by the same corporation. Duty does not come into it.</summary>
    public static bool AreColleagues(int a, int b)
    {
        if (a <= 0 || b <= 0)
            return false;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT 1 FROM `rp_corporation_employees` x " +
            "JOIN `rp_corporation_employees` y ON y.`corporation_id` = x.`corporation_id` " +
            "WHERE x.`user_id` = @a AND y.`user_id` = @b LIMIT 1");
        dbClient.AddParameter("a", a);
        dbClient.AddParameter("b", b);
        return dbClient.GetRow() != null;
    }
}
