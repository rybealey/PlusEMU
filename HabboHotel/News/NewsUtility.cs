using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.Utilities;

namespace Plus.HabboHotel.News;

/// <summary>
/// pixelrp: the phone's News app - a staff-run noticeboard. Everyone reads
/// the latest stories; staff (rank 5+) post, authors (or senior staff) edit
/// and delete, any staff member pins. One pinned story at a time. Every
/// change re-sends the feed to everyone online, composed per client so the
/// canPost flag is theirs. Rows are property classes for Dapper.
/// </summary>
public static class NewsUtility
{
    public const int StaffRank = 5;
    public const int SeniorRank = 6;
    public const int MaxPosts = 50;
    public const int MaxTitle = 120;
    public const int MaxBody = 4000;
    public const int MaxCategory = 24;
    public const int MaxImage = 160;

    public static readonly string[] Categories = { "City Hall", "Events", "Crime", "Business" };

    /// <summary>The newsroom byline: stories published anonymously show as written by this account.</summary>
    public const string BylineName = "Trina";

    public class PostRow
    {
        public int Id { get; set; }
        public int AuthorId { get; set; }
        public string AuthorName { get; set; } = "";
        public string AuthorFigure { get; set; } = "";
        public int Anonymous { get; set; }
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string Image { get; set; } = "";
        public int Pinned { get; set; }
        public int CreatedAt { get; set; }
        public int UpdatedAt { get; set; }
    }

    public static int Now() => (int)UnixTimestamp.GetNow();
    public static bool IsStaff(Habbo habbo) => habbo != null && habbo.Rank >= StaffRank;
    public static bool IsSenior(Habbo habbo) => habbo != null && habbo.Rank >= SeniorRank;

    public static string CleanCategory(string raw)
    {
        var trimmed = (raw ?? "").Trim();
        foreach (var c in Categories)
            if (string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase)) return c;
        return Categories[0];
    }

    /// <summary>A library file name only: no paths, no odd characters.</summary>
    public static string CleanImage(string raw)
    {
        var name = (raw ?? "").Trim();
        if (name.Length == 0 || name.Length > MaxImage) return "";
        foreach (var ch in name)
            if (!(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.')) return "";
        if (name.Contains("..")) return "";
        var lower = name.ToLowerInvariant();
        return (lower.EndsWith(".png") || lower.EndsWith(".gif") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".webp")) ? name : "";
    }

    public static List<PostRow> GetPosts()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<PostRow>(
            "SELECT p.`id` AS Id, p.`author_id` AS AuthorId, COALESCE(u.`username`, '') AS AuthorName, COALESCE(u.`look`, '') AS AuthorFigure, p.`anonymous` AS Anonymous, p.`category` AS Category, p.`title` AS Title, p.`body` AS Body, p.`image` AS Image, p.`pinned` AS Pinned, p.`created_at` AS CreatedAt, p.`updated_at` AS UpdatedAt " +
            "FROM `rp_news_posts` p LEFT JOIN `users` u ON u.`id` = p.`author_id` ORDER BY p.`pinned` DESC, p.`created_at` DESC LIMIT @limit",
            new { limit = MaxPosts }).ToList();
    }

    public static PostRow GetPost(int id)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<PostRow>(
            "SELECT p.`id` AS Id, p.`author_id` AS AuthorId, COALESCE(u.`username`, '') AS AuthorName, COALESCE(u.`look`, '') AS AuthorFigure, p.`anonymous` AS Anonymous, p.`category` AS Category, p.`title` AS Title, p.`body` AS Body, p.`image` AS Image, p.`pinned` AS Pinned, p.`created_at` AS CreatedAt, p.`updated_at` AS UpdatedAt " +
            "FROM `rp_news_posts` p LEFT JOIN `users` u ON u.`id` = p.`author_id` WHERE p.`id` = @id", new { id });
    }

    /// <summary>0 = reader, 1 = staff (post, pin, own stories), 2 = senior (edit or delete anyone's).</summary>
    public static int StaffLevel(Habbo habbo) => IsSenior(habbo) ? 2 : (IsStaff(habbo) ? 1 : 0);

    public class BylineRow { public int Id { get; set; } public string Username { get; set; } = ""; public string Figure { get; set; } = ""; }

    private static BylineRow _byline;
    private static DateTime _bylineAt = DateTime.MinValue;

    /// <summary>The Trina account (id, name, figure), looked up at most once a minute; a name-only stand-in if no such user exists.</summary>
    public static BylineRow GetByline()
    {
        if (_byline != null && (DateTime.UtcNow - _bylineAt).TotalSeconds < 60) return _byline;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var row = connection.QueryFirstOrDefault<BylineRow>(
            "SELECT `id` AS Id, `username` AS Username, COALESCE(`look`, '') AS Figure FROM `users` WHERE `username` = @name LIMIT 1", new { name = BylineName });
        _byline = row ?? new BylineRow { Id = 0, Username = BylineName, Figure = "" };
        _bylineAt = DateTime.UtcNow;
        return _byline;
    }

    public static RpNewsComposer Compose(GameClient session, List<PostRow> posts) => new(StaffLevel(session.GetHabbo()), GetByline(), posts);

    public static void SendNews(GameClient session) => session.Send(Compose(session, GetPosts()));

    public static void BroadcastNews()
    {
        var posts = GetPosts();
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
        {
            if (client?.GetHabbo() == null) continue;
            client.Send(Compose(client, posts));
        }
    }
}
