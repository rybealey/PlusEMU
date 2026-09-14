using Dapper;
using Plus.Communication.Packets.Outgoing.Users.Sitch;
using Plus.HabboHotel.GameClients;
using Plus.Utilities;

namespace Plus.HabboHotel.Sitch;

/// <summary>
/// pixelrp: Sitch, the city's own feed.
///
/// Posts are short, public and permanent. A reply IS a post with a parent, so
/// likes, reposts and deletion need no second set of tables and a reply can be
/// replied to without anything new. Deletion is soft throughout - a removed
/// post keeps its row so staff removals stay auditable - which is why every
/// read here carries `deleted_at` = 0.
///
/// READ PATH ONLY for now: nothing in this file writes. The counts each post
/// carries (replies, likes, reposts) and the two "did I" flags are computed in
/// the query rather than cached on the row, because a cached counter that
/// drifts is worse than a join, and at hotel scale this is a handful of rows.
///
/// Row types are property classes for Dapper, and double as the composers'
/// payload types - the same arrangement NotesUtility uses.
/// </summary>
public static class SitchUtility
{
    /// <summary>The post length the composer enforces and the server re-checks.</summary>
    public const int MaxBody = 280;

    /// <summary>How much of a timeline one fetch returns.</summary>
    public const int FeedPageSize = 40;

    /// <summary>How far back the Activity tab reaches.</summary>
    public const int ActivityPageSize = 50;

    public class PostRow
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Figure { get; set; } = "";
        public int Rank { get; set; }
        public string Body { get; set; } = "";
        public int PhotoId { get; set; }
        /// <summary>Empty when the post carries no photo, or the photo is gone.</summary>
        public string PhotoUrl { get; set; } = "";
        public string PhotoRoom { get; set; } = "";
        public int CreatedAt { get; set; }
        public int Replies { get; set; }
        public int Likes { get; set; }
        public int Reposts { get; set; }
        /// <summary>1 when the VIEWER has liked / reposted this, not the author.</summary>
        public int Liked { get; set; }
        public int Reposted { get; set; }
    }

    public class ProfileRow
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Figure { get; set; } = "";
        public string Motto { get; set; } = "";
        public string Bio { get; set; } = "";
        public string FavoriteVideoId { get; set; } = "";
        public string FavoriteTitle { get; set; } = "";
        public string FavoriteAuthor { get; set; } = "";
        public int Followers { get; set; }
        public int Following { get; set; }
        /// <summary>1 when the viewer follows this person.</summary>
        public int Follows { get; set; }
    }

    public class ActivityRow
    {
        public int Id { get; set; }
        public int ActorId { get; set; }
        public string ActorName { get; set; } = "";
        public string ActorFigure { get; set; } = "";
        public string Kind { get; set; } = "";
        public int PostId { get; set; }
        /// <summary>A little of the post it happened to, for context. Empty for a follow.</summary>
        public string PostBody { get; set; } = "";
        public int CreatedAt { get; set; }
        public int Seen { get; set; }
    }

    public static int Now() => (int)UnixTimestamp.GetNow();

    // ---- reads --------------------------------------------------------------

    /// <summary>
    /// The columns every post carries, with the viewer's own like/repost state.
    ///
    /// @viewerId appears in the two EXISTS clauses only - the counts belong to
    /// the post, the flags belong to whoever is looking.
    /// </summary>
    private const string PostSelect =
        "SELECT p.`id` AS Id, p.`parent_id` AS ParentId, p.`user_id` AS UserId, " +
        "u.`username` AS Username, COALESCE(u.`look`, '') AS Figure, COALESCE(u.`rank`, 1) AS Rank, " +
        "p.`body` AS Body, p.`photo_id` AS PhotoId, " +
        "COALESCE(c.`url`, '') AS PhotoUrl, COALESCE(c.`room_name`, '') AS PhotoRoom, " +
        "p.`created_at` AS CreatedAt, " +
        "(SELECT COUNT(*) FROM `rp_sitch_posts` r WHERE r.`parent_id` = p.`id` AND r.`deleted_at` = 0) AS Replies, " +
        "(SELECT COUNT(*) FROM `rp_sitch_likes` l WHERE l.`post_id` = p.`id`) AS Likes, " +
        "(SELECT COUNT(*) FROM `rp_sitch_reposts` s WHERE s.`post_id` = p.`id`) AS Reposts, " +
        "EXISTS(SELECT 1 FROM `rp_sitch_likes` ml WHERE ml.`post_id` = p.`id` AND ml.`user_id` = @viewerId) AS Liked, " +
        "EXISTS(SELECT 1 FROM `rp_sitch_reposts` ms WHERE ms.`post_id` = p.`id` AND ms.`user_id` = @viewerId) AS Reposted " +
        "FROM `rp_sitch_posts` p " +
        "INNER JOIN `users` u ON u.`id` = p.`user_id` " +
        "LEFT JOIN `camera_web` c ON c.`id` = p.`photo_id` ";

    /// <summary>
    /// A timeline. `following` narrows it to people the viewer follows, plus
    /// the viewer's own posts - a Following tab that hid your own voice would
    /// read as broken the first time somebody posted into it.
    /// </summary>
    public static List<PostRow> GetFeed(int viewerId, bool following)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var scope = following
            ? "AND (p.`user_id` = @viewerId OR EXISTS(SELECT 1 FROM `rp_sitch_follows` f WHERE f.`follower_id` = @viewerId AND f.`followee_id` = p.`user_id`)) "
            : "";
        return connection.Query<PostRow>(
            PostSelect + "WHERE p.`parent_id` = 0 AND p.`deleted_at` = 0 " + scope +
            "ORDER BY p.`created_at` DESC, p.`id` DESC LIMIT @limit",
            new { viewerId, limit = FeedPageSize }).ToList();
    }

    /// <summary>
    /// One post and its replies. The parent comes back first whether or not it
    /// is still live - a thread whose root was removed still has readable
    /// replies, and dropping the root silently would leave them orphaned on
    /// screen with no explanation.
    /// </summary>
    public static List<PostRow> GetThread(int viewerId, int postId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<PostRow>(
            PostSelect + "WHERE (p.`id` = @postId OR p.`parent_id` = @postId) AND p.`deleted_at` = 0 " +
            "ORDER BY (p.`id` = @postId) DESC, p.`created_at` ASC, p.`id` ASC LIMIT @limit",
            new { viewerId, postId, limit = FeedPageSize }).ToList();
    }

    /// <summary>Somebody's profile, as the viewer sees it.</summary>
    public static ProfileRow GetProfile(int viewerId, int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<ProfileRow>(
            "SELECT u.`id` AS UserId, u.`username` AS Username, COALESCE(u.`look`, '') AS Figure, " +
            "COALESCE(u.`motto`, '') AS Motto, " +
            "COALESCE(s.`bio`, '') AS Bio, COALESCE(s.`favorite_video_id`, '') AS FavoriteVideoId, " +
            "COALESCE(s.`favorite_title`, '') AS FavoriteTitle, COALESCE(s.`favorite_author`, '') AS FavoriteAuthor, " +
            "(SELECT COUNT(*) FROM `rp_sitch_follows` a WHERE a.`followee_id` = u.`id`) AS Followers, " +
            "(SELECT COUNT(*) FROM `rp_sitch_follows` b WHERE b.`follower_id` = u.`id`) AS Following, " +
            "EXISTS(SELECT 1 FROM `rp_sitch_follows` m WHERE m.`follower_id` = @viewerId AND m.`followee_id` = u.`id`) AS Follows " +
            "FROM `users` u LEFT JOIN `rp_sitch_profiles` s ON s.`user_id` = u.`id` WHERE u.`id` = @userId",
            new { viewerId, userId });
    }

    /// <summary>A profile's own posts, newest first.</summary>
    public static List<PostRow> GetUserPosts(int viewerId, int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<PostRow>(
            PostSelect + "WHERE p.`user_id` = @userId AND p.`parent_id` = 0 AND p.`deleted_at` = 0 " +
            "ORDER BY p.`created_at` DESC, p.`id` DESC LIMIT @limit",
            new { viewerId, userId, limit = FeedPageSize }).ToList();
    }

    /// <summary>What happened to the viewer while they were away.</summary>
    public static List<ActivityRow> GetActivity(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<ActivityRow>(
            "SELECT a.`id` AS Id, a.`actor_id` AS ActorId, u.`username` AS ActorName, " +
            "COALESCE(u.`look`, '') AS ActorFigure, a.`kind` AS Kind, a.`post_id` AS PostId, " +
            "COALESCE(SUBSTRING(p.`body`, 1, 80), '') AS PostBody, a.`created_at` AS CreatedAt, a.`seen` AS Seen " +
            "FROM `rp_sitch_activity` a " +
            "INNER JOIN `users` u ON u.`id` = a.`actor_id` " +
            "LEFT JOIN `rp_sitch_posts` p ON p.`id` = a.`post_id` " +
            "WHERE a.`user_id` = @userId ORDER BY a.`created_at` DESC, a.`id` DESC LIMIT @limit",
            new { userId, limit = ActivityPageSize }).ToList();
    }

    // ---- pushes -------------------------------------------------------------

    public static void SendFeed(GameClient session, bool following)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null) return;
        session.Send(new RpSitchFeedComposer(following, GetFeed(habbo.Id, following)));
    }

    public static void SendThread(GameClient session, int postId)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null) return;
        session.Send(new RpSitchThreadComposer(postId, GetThread(habbo.Id, postId)));
    }

    public static void SendProfile(GameClient session, int userId)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null) return;
        var profile = GetProfile(habbo.Id, userId);
        if (profile == null) return;
        session.Send(new RpSitchProfileComposer(profile, GetUserPosts(habbo.Id, userId)));
    }

    public static void SendActivity(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null) return;
        session.Send(new RpSitchActivityComposer(GetActivity(habbo.Id)));
    }

    /// <summary>
    /// Push a fresh feed to everyone online who is looking at Sitch. Used by
    /// the write path; harmless before it exists.
    /// </summary>
    public static void SendFeedTo(IEnumerable<int> userIds)
    {
        foreach (var userId in userIds.Distinct())
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
            if (client?.GetHabbo() != null) SendFeed(client, false);
        }
    }
}
