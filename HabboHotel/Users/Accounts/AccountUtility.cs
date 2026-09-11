using Plus.Database.Interfaces;

namespace Plus.HabboHotel.Users.Accounts;

/// <summary>
/// pixelrp: one account, up to three characters.
///
/// A character is a `users` row like any other; what makes several of them one
/// account is `parent_id`. Depth is exactly one - a child can never itself be
/// a root - so resolving an account is `parent_id ?? id` and nothing walks a
/// tree.
///
/// Credentials live only on the root. There is no way to log in AS a child;
/// the CMS authenticates the root and issues the SSO ticket for whichever
/// character `active_character_id` names, which is simply the last one played.
///
/// Everything here reads live rather than caching. An account has at most
/// three rows and these run on login, on the Wallet opening, and on a create
/// or a switch - never in a loop.
/// </summary>
public static class AccountUtility
{
    /// <summary>Hard limit. Not a setting, not grantable, not purchasable.</summary>
    public const int MaxCharacters = 3;

    public record Character(int Id, string Username, string Look, string Motto, string Gender);

    /// <summary>The account a user belongs to: their root, or themselves.</summary>
    public static int RootOf(int userId)
    {
        if (userId <= 0)
            return 0;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        return RootOf(dbClient, userId);
    }

    private static int RootOf(IQueryAdapter dbClient, int userId)
    {
        dbClient.SetQuery("SELECT COALESCE(`parent_id`, `id`) FROM `users` WHERE `id` = @id LIMIT 1");
        dbClient.AddParameter("id", userId);
        var root = dbClient.GetInteger();
        return (root == 0) ? userId : root;
    }

    /// <summary>
    /// The one question every anti-abuse check asks. Two characters of one
    /// person must not trade, befriend, employ, gang up with or police each
    /// other - all of which is this, answered once.
    /// </summary>
    public static bool SameAccount(int a, int b)
    {
        if (a <= 0 || b <= 0)
            return false;
        if (a == b)
            return true;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        return RootOf(dbClient, a) == RootOf(dbClient, b);
    }

    /// <summary>Every character on this user's account, root first, then by id.</summary>
    public static List<Character> Characters(int userId)
    {
        var characters = new List<Character>();
        if (userId <= 0)
            return characters;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        var root = RootOf(dbClient, userId);
        dbClient.SetQuery(
            "SELECT `id`, `username`, `look`, `motto`, `gender` FROM `users` " +
            "WHERE `id` = @root OR `parent_id` = @root " +
            "ORDER BY (`id` = @root) DESC, `id` ASC");
        dbClient.AddParameter("root", root);
        var table = dbClient.GetTable();
        if (table == null)
            return characters;
        foreach (System.Data.DataRow row in table.Rows)
        {
            characters.Add(new Character(
                Convert.ToInt32(row["id"]),
                Convert.ToString(row["username"]) ?? "",
                Convert.ToString(row["look"]) ?? "",
                Convert.ToString(row["motto"]) ?? "",
                (Convert.ToString(row["gender"]) ?? "M").ToUpperInvariant()));
        }
        return characters;
    }

    /// <summary>
    /// Point the account at a character, so the next /game load enters as
    /// them. Written on the ROOT row, which is the one the CMS authenticates.
    /// </summary>
    public static void SetActive(int userId, int characterId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        var root = RootOf(dbClient, userId);
        dbClient.SetQuery("UPDATE `users` SET `active_character_id` = @character WHERE `id` = @root LIMIT 1");
        dbClient.AddParameter("character", characterId);
        dbClient.AddParameter("root", root);
        dbClient.RunQuery();
    }

    /// <summary>
    /// Why a name cannot be used, or null when it can. The rules are the
    /// website's own, read from the same rows registration reads - the regex
    /// and wordfilter are settings and a table, not PHP, so there is one list
    /// rather than two that drift.
    /// </summary>
    public static string RejectName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length < 3)
            return "That name is too short.";
        if (name.Length > 25)
            return "That name is too long.";

        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();

        var pattern = Setting(dbClient, "username_regex");
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            // Stored in PHP's /.../ form; C# wants the pattern without the
            // delimiters. A malformed one must not make names unusable, so a
            // bad pattern falls through to the conservative default below.
            var trimmed = pattern.Trim();
            if (trimmed.Length > 2 && trimmed[0] == '/')
                trimmed = trimmed[1..trimmed.LastIndexOf('/')];
            try
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, trimmed))
                    return "Letters, numbers and _ . - only.";
            }
            catch (ArgumentException)
            {
                pattern = null;
            }
        }
        if (string.IsNullOrWhiteSpace(pattern) &&
            !System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-zA-Z0-9_.-]+$"))
            return "Letters, numbers and _ . - only.";

        if (Setting(dbClient, "website_wordfilter_enabled") == "1")
        {
            dbClient.SetQuery("SELECT 1 FROM `website_wordfilter` WHERE LOCATE(LOWER(`word`), LOWER(@name)) > 0 LIMIT 1");
            dbClient.AddParameter("name", name);
            if (dbClient.GetRow() != null)
                return "That name is not allowed.";
        }

        dbClient.SetQuery("SELECT 1 FROM `users` WHERE `username` = @name LIMIT 1");
        dbClient.AddParameter("name", name);
        if (dbClient.GetRow() != null)
            return $"Somebody already plays as {name}.";

        return null;
    }

    /// <summary>
    /// Make a character on this account. Returns its id, or 0 if the account
    /// is full - the name is checked by the caller, which has a message to
    /// show for each way it can fail.
    /// </summary>
    public static int Create(int userId, string name, string gender)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        var root = RootOf(dbClient, userId);

        dbClient.SetQuery("SELECT COUNT(*) FROM `users` WHERE `id` = @root OR `parent_id` = @root");
        dbClient.AddParameter("root", root);
        if (dbClient.GetInteger() >= MaxCharacters)
            return 0;

        gender = (gender == "F") ? "F" : "M";
        var look = Setting(dbClient, (gender == "F") ? "start_look_female" : "start_look_male")
                   ?? Setting(dbClient, "start_look")
                   ?? "hr-100-61.hd-180-1.ch-210-66.lg-270-110.sh-305-62";
        var motto = Setting(dbClient, "start_motto") ?? "Welcome to the hotel!";
        var credits = Setting(dbClient, "start_credits");
        var homeRoom = Setting(dbClient, "hotel_home_room");

        // No mail and an unusable password: a character is entered through its
        // account, never logged into directly. `auth_ticket` stays empty - the
        // CMS mints one when this character is the active one.
        dbClient.SetQuery(
            "INSERT INTO `users` (`parent_id`,`username`,`real_name`,`password`,`mail`,`auth_ticket`," +
            "`account_created`,`last_login`,`last_online`,`motto`,`look`,`gender`,`rank`,`credits`,`home_room`) " +
            "VALUES (@root,@name,'','!','',''," +
            "UNIX_TIMESTAMP(),UNIX_TIMESTAMP(),UNIX_TIMESTAMP(),@motto,@look,@gender,1,@credits,@home)");
        dbClient.AddParameter("root", root);
        dbClient.AddParameter("name", name.Trim());
        dbClient.AddParameter("motto", motto);
        dbClient.AddParameter("look", look);
        dbClient.AddParameter("gender", gender);
        dbClient.AddParameter("credits", string.IsNullOrWhiteSpace(credits) ? 1000 : Convert.ToInt32(credits));
        dbClient.AddParameter("home", string.IsNullOrWhiteSpace(homeRoom) ? 0 : Convert.ToInt32(homeRoom));
        var id = (int)dbClient.InsertQuery();

        if (id > 0)
        {
            dbClient.SetQuery("INSERT IGNORE INTO `user_statistics` (`id`) VALUES (@id)");
            dbClient.AddParameter("id", id);
            dbClient.RunQuery();
        }
        return id;
    }

    private static string Setting(IQueryAdapter dbClient, string key)
    {
        dbClient.SetQuery("SELECT `value` FROM `website_settings` WHERE `key` = @key LIMIT 1");
        dbClient.AddParameter("key", key);
        var row = dbClient.GetRow();
        return (row == null) ? null : Convert.ToString(row["value"]);
    }
}
