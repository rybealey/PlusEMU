using System.Collections.Concurrent;
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
/// The counts each post carries (replies, likes, reposts) and the two "did I"
/// flags are computed in the query rather than cached on the row, because a
/// cached counter that drifts is worse than a join, and at hotel scale this is
/// a handful of rows.
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
        // An affordance flag only - RpSitchDeleteEvent checks the permission
        // again before removing anything.
        var canModerate = habbo.Permissions?.HasCommand("rp_sitch_moderate") ?? false;
        session.Send(new RpSitchFeedComposer(following, canModerate, GetFeed(habbo.Id, following)));
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

    // ---- writes -------------------------------------------------------------

    /// <summary>
    /// How long a player must wait between posts.
    ///
    /// The same shape :hit uses - a dictionary of last-action times rather
    /// than a table, because a cooldown that resets when the server restarts
    /// is fine and a row per post attempt is not.
    /// </summary>
    public const int PostCooldownSeconds = 20;

    private static readonly ConcurrentDictionary<int, int> LastPost = new();

    /// <summary>Seconds still to wait, or 0 when the player may post.</summary>
    public static int CooldownLeft(int userId)
    {
        if (!LastPost.TryGetValue(userId, out var last)) return 0;
        var left = PostCooldownSeconds - (Now() - last);
        return (left > 0) ? left : 0;
    }

    /// <summary>
    /// Does this photo belong to this player? Checked before a post may carry
    /// it, so nobody can attach somebody else's picture by guessing an id.
    /// </summary>
    public static bool OwnsPhoto(int userId, int photoId)
    {
        if (photoId <= 0) return false;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM `camera_web` WHERE `id` = @photoId AND `user_id` = @userId",
            new { photoId, userId }) > 0;
    }

    /// <summary>The author of a live post, or 0 if it is gone.</summary>
    public static int AuthorOf(int postId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<int>(
            "SELECT COALESCE((SELECT `user_id` FROM `rp_sitch_posts` WHERE `id` = @postId AND `deleted_at` = 0), 0)",
            new { postId });
    }

    /// <summary>Returns the new post's id, or 0 if the parent has gone.</summary>
    public static int CreatePost(int userId, string body, int parentId, int photoId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();

        // A reply to a post that has been removed would be unreachable - the
        // thread it belongs to no longer lists it - so it is refused rather
        // than written somewhere nobody can read.
        if (parentId > 0 && connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM `rp_sitch_posts` WHERE `id` = @parentId AND `deleted_at` = 0",
                new { parentId }) == 0)
            return 0;

        var now = Now();
        LastPost[userId] = now;

        var id = connection.ExecuteScalar<int>(
            "INSERT INTO `rp_sitch_posts` (`user_id`,`parent_id`,`body`,`photo_id`,`created_at`) " +
            "VALUES (@userId,@parentId,@body,@photoId,@now); SELECT LAST_INSERT_ID();",
            new { userId, parentId, body, photoId, now });

        if (parentId > 0)
        {
            var parentAuthor = connection.ExecuteScalar<int>(
                "SELECT COALESCE((SELECT `user_id` FROM `rp_sitch_posts` WHERE `id` = @parentId), 0)", new { parentId });
            AddActivity(parentAuthor, userId, "reply", id);
        }

        return id;
    }

    /// <summary>
    /// Like or unlike. INSERT IGNORE / DELETE rather than a read-then-write:
    /// the composite primary key already says a player likes a post at most
    /// once, so the database settles a double tap instead of a race.
    /// </summary>
    public static void SetLike(int postId, int userId, bool on)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        if (on)
        {
            var added = connection.Execute(
                "INSERT IGNORE INTO `rp_sitch_likes` (`post_id`,`user_id`,`created_at`) VALUES (@postId,@userId,@now)",
                new { postId, userId, now = Now() });
            // Only a NEW like is worth telling somebody about, so a player
            // cannot spam a notification by tapping twice.
            if (added > 0) AddActivity(AuthorOf(postId), userId, "like", postId);
        }
        else
            connection.Execute("DELETE FROM `rp_sitch_likes` WHERE `post_id` = @postId AND `user_id` = @userId",
                new { postId, userId });
    }

    public static void SetRepost(int postId, int userId, bool on)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        if (on)
        {
            var added = connection.Execute(
                "INSERT IGNORE INTO `rp_sitch_reposts` (`post_id`,`user_id`,`created_at`) VALUES (@postId,@userId,@now)",
                new { postId, userId, now = Now() });
            if (added > 0) AddActivity(AuthorOf(postId), userId, "repost", postId);
        }
        else
            connection.Execute("DELETE FROM `rp_sitch_reposts` WHERE `post_id` = @postId AND `user_id` = @userId",
                new { postId, userId });
    }

    public static void SetFollow(int followerId, int followeeId, bool on)
    {
        if (followerId == followeeId) return;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        if (on)
        {
            var added = connection.Execute(
                "INSERT IGNORE INTO `rp_sitch_follows` (`follower_id`,`followee_id`,`created_at`) VALUES (@followerId,@followeeId,@now)",
                new { followerId, followeeId, now = Now() });
            if (added > 0) AddActivity(followeeId, followerId, "follow", 0);
        }
        else
            connection.Execute("DELETE FROM `rp_sitch_follows` WHERE `follower_id` = @followerId AND `followee_id` = @followeeId",
                new { followerId, followeeId });
    }

    /// <summary>
    /// Soft-delete a post. The author may remove their own; staff may remove
    /// anyone's. The row stays, carrying who removed it and when, so a staff
    /// removal is auditable afterwards.
    /// </summary>
    public static bool DeletePost(int postId, int actorId, bool staff)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var sql = "UPDATE `rp_sitch_posts` SET `deleted_at` = @now, `deleted_by` = @actorId " +
                  "WHERE `id` = @postId AND `deleted_at` = 0" + (staff ? "" : " AND `user_id` = @actorId");
        return connection.Execute(sql, new { postId, actorId, now = Now() }) > 0;
    }

    /// <summary>
    /// The favorite song, or all-empty to clear it. Title and author are what
    /// oEmbed returned at save time, kept so a profile renders without a
    /// network call every time somebody opens it.
    /// </summary>
    public static void SetFavoriteSong(int userId, string videoId, string title, string author)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute(
            "INSERT INTO `rp_sitch_profiles` (`user_id`,`favorite_video_id`,`favorite_title`,`favorite_author`,`updated_at`) " +
            "VALUES (@userId,@videoId,@title,@author,@now) " +
            "ON DUPLICATE KEY UPDATE `favorite_video_id` = @videoId, `favorite_title` = @title, " +
            "`favorite_author` = @author, `updated_at` = @now",
            new { userId, videoId, title, author, now = Now() });
    }

    public static void SetBio(int userId, string bio)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute(
            "INSERT INTO `rp_sitch_profiles` (`user_id`,`bio`,`updated_at`) VALUES (@userId,@bio,@now) " +
            "ON DUPLICATE KEY UPDATE `bio` = @bio, `updated_at` = @now",
            new { userId, bio, now = Now() });
    }

    /// <summary>
    /// Record something that happened TO somebody. Never records an action
    /// against yourself - "you liked your own post" is noise, not activity.
    /// </summary>
    public static void AddActivity(int userId, int actorId, string kind, int postId)
    {
        if (userId <= 0 || actorId <= 0 || userId == actorId) return;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute(
            "INSERT INTO `rp_sitch_activity` (`user_id`,`actor_id`,`kind`,`post_id`,`created_at`) " +
            "VALUES (@userId,@actorId,@kind,@postId,@now)",
            new { userId, actorId, kind, postId, now = Now() });
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
