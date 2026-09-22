using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.News;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Support;

/// <summary>
/// pixelrp: the Support app's queue and its round-robin.
///
/// A player opens a conversation and it joins ONE queue - every category, every
/// rank. It is then OFFERED to staff in turn: one at a time, for a few seconds
/// each, until somebody takes it.
///
/// THE PLAYER ONLY EVER TALKS TO TRINA. The same byline the News app publishes
/// anonymous stories under (NewsUtility.BylineName): who actually answers, and
/// the fact that it can change hands mid-conversation, never reaches them.
/// Staff see the real names throughout. That is what makes a silent
/// reassignment possible at all - there is nothing in the player's thread that
/// could give it away.
///
/// WHY THE ROTATION IS DERIVED, NOT STORED. There is no cursor. "Next" is the
/// available staff member with the oldest last_offered_at, which means nobody
/// has to fix the pointer when somebody clocks on, clocks off, or is removed
/// mid-rotation - the order simply re-forms around whoever is there. A stored
/// index would need every one of those cases handled, and the one that gets
/// missed silently parks the rotation on somebody who has gone.
///
/// The pointer advances on the OFFER, not on the resolve, so one long chat
/// never blocks the queue behind it.
/// </summary>
public static class SupportUtility
{
    /// <summary>Rank at which somebody can answer support chats.</summary>
    public const int StaffRank = 5;

    /// <summary>Seconds a staff member has to take an offered chat before it moves on.</summary>
    public const int OfferSeconds = 20;

    /// <summary>Lapsed offers in a row before a staff member is switched off.</summary>
    public const int MissesBeforeAway = 2;

    /// <summary>Open chats one staff member may hold at once.</summary>
    public const int OpenChatCap = 3;

    /// <summary>A player may have this many conversations going, so the queue cannot be flooded.</summary>
    public const int PlayerOpenCap = 2;

    public const int MaxBodyLength = 1000;

    public static readonly string[] Categories = { "report", "broken", "appeal", "other" };

    public static bool IsStaff(Habbo? habbo) => habbo != null && habbo.Rank >= StaffRank;

    public static string CleanCategory(string? raw)
    {
        var trimmed = (raw ?? "").Trim().ToLowerInvariant();
        foreach (var c in Categories)
            if (c == trimmed) return c;
        return "other";
    }

    private static int Now() => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // ---- rows ---------------------------------------------------------------
    // Property classes, never positional records: the ids are unsigned in
    // places and Dapper binds these by name.

    public class ThreadRow
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public string Category { get; set; } = "other";
        public string Status { get; set; } = "waiting";
        public int StaffId { get; set; }
        public int OfferedUntil { get; set; }
        public int Offers { get; set; }
        public int CreatedAt { get; set; }
        public int UpdatedAt { get; set; }
        /// <summary>The player who opened it. Staff-side only - never composed to a player.</summary>
        public string PlayerName { get; set; } = "";
        /// <summary>Whoever holds it. Staff-side ONLY: composing this to a player is the leak the byline exists to prevent.</summary>
        public string StaffName { get; set; } = "";
    }

    public class MessageRow
    {
        public int Id { get; set; }
        public int ThreadId { get; set; }
        public int AuthorId { get; set; }
        public int FromStaff { get; set; }
        public string Body { get; set; } = "";
        public int CreatedAt { get; set; }
    }

    public class RotationRow
    {
        public int UserId { get; set; }
        public int Available { get; set; }
        public int LastOfferedAt { get; set; }
        public int Missed { get; set; }
    }

    // ---- availability -------------------------------------------------------

    /// <summary>
    /// Put a staff member in or out of the rotation. Coming ON clears their
    /// missed count: the count exists to notice somebody who has walked away,
    /// and saying "I am here" is the answer to that.
    /// </summary>
    public static void SetAvailable(int userId, bool available)
    {
        if (userId <= 0)
            return;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute(
            "INSERT INTO `rp_support_rotation` (`user_id`,`available`,`last_offered_at`,`missed`,`updated_at`) " +
            "VALUES (@userId, @available, 0, 0, @now) " +
            "ON DUPLICATE KEY UPDATE `available` = @available, `missed` = IF(@available = 1, 0, `missed`), `updated_at` = @now",
            new { userId, available = available ? 1 : 0, now = Now() });
        if (available)
            Wake();
    }

    public static bool IsAvailable(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM `rp_support_rotation` WHERE `user_id` = @userId AND `available` = 1",
            new { userId }) > 0;
    }

    // ---- the player's side --------------------------------------------------

    /// <summary>
    /// Open a conversation. Returns the new thread's id, or 0 when the player
    /// already has as many open as they are allowed.
    /// </summary>
    public static int StartThread(int playerId, string category, string body)
    {
        var text = Clean(body);
        if (playerId <= 0 || text.Length == 0)
            return 0;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var open = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM `rp_support_threads` WHERE `player_id` = @playerId AND `status` <> 'resolved'",
            new { playerId });
        if (open >= PlayerOpenCap)
            return 0;

        var now = Now();
        connection.Execute(
            "INSERT INTO `rp_support_threads` (`player_id`,`category`,`status`,`created_at`,`updated_at`) " +
            "VALUES (@playerId, @category, 'waiting', @now, @now)",
            new { playerId, category = CleanCategory(category), now });
        var threadId = connection.ExecuteScalar<int>("SELECT LAST_INSERT_ID()");
        if (threadId <= 0)
            return 0;
        AddMessage(threadId, playerId, false, text);
        Wake();
        return threadId;
    }

    /// <summary>
    /// Append a message. `fromStaff` decides which side it renders on; the
    /// author id is always the real person, staff included, because a
    /// moderation log that says "Trina" is a log of nothing.
    /// </summary>
    public static void AddMessage(int threadId, int authorId, bool fromStaff, string body)
    {
        var text = Clean(body);
        if (threadId <= 0 || authorId <= 0 || text.Length == 0)
            return;
        var now = Now();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute(
            "INSERT INTO `rp_support_messages` (`thread_id`,`author_id`,`from_staff`,`body`,`created_at`) " +
            "VALUES (@threadId, @authorId, @fromStaff, @body, @now)",
            new { threadId, authorId, fromStaff = fromStaff ? 1 : 0, body = text, now });
        connection.Execute("UPDATE `rp_support_threads` SET `updated_at` = @now WHERE `id` = @threadId",
            new { threadId, now });
    }

    public static List<ThreadRow> ThreadsForPlayer(int playerId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<ThreadRow>(
            "SELECT `id` AS Id, `player_id` AS PlayerId, `category` AS Category, `status` AS Status, " +
            "`staff_id` AS StaffId, `offered_until` AS OfferedUntil, `offers` AS Offers, " +
            "`created_at` AS CreatedAt, `updated_at` AS UpdatedAt " +
            "FROM `rp_support_threads` WHERE `player_id` = @playerId ORDER BY `updated_at` DESC LIMIT 40",
            new { playerId }).ToList();
    }

    public static List<MessageRow> MessagesFor(int threadId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<MessageRow>(
            "SELECT `id` AS Id, `thread_id` AS ThreadId, `author_id` AS AuthorId, `from_staff` AS FromStaff, " +
            "`body` AS Body, `created_at` AS CreatedAt " +
            "FROM `rp_support_messages` WHERE `thread_id` = @threadId ORDER BY `id` ASC LIMIT 200",
            new { threadId }).ToList();
    }

    public static ThreadRow? Thread(int threadId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<ThreadRow>(
            "SELECT `id` AS Id, `player_id` AS PlayerId, `category` AS Category, `status` AS Status, " +
            "`staff_id` AS StaffId, `offered_until` AS OfferedUntil, `offers` AS Offers, " +
            "`created_at` AS CreatedAt, `updated_at` AS UpdatedAt " +
            "FROM `rp_support_threads` WHERE `id` = @threadId LIMIT 1",
            new { threadId }).FirstOrDefault();
    }

    private static string Clean(string? raw)
    {
        var text = (raw ?? "").Replace('\r', ' ').Trim();
        return text.Length > MaxBodyLength ? text.Substring(0, MaxBodyLength) : text;
    }

    // ---- the rotation -------------------------------------------------------

    private static Thread? _worker;
    private static readonly object _workerSync = new();
    private static readonly ManualResetEventSlim _wake = new(false);

    /// <summary>Something happened that could give the rotation work; look now.</summary>
    private static void Wake() => _wake.Set();

    /// <summary>
    /// Start the rotation's own thread. Called once at startup.
    ///
    /// IT IS NOT ON THE GAME LOOP, and that is the entire point of this
    /// method. The loop runs every 5ms on ONE thread that ticks every room and
    /// every client, and its own comment says it: either call blocking there
    /// delays every room tick. This work is a handful of database round trips
    /// a second, which is precisely the thing that must not sit on it - a slow
    /// query would stall the whole hotel, not just the queue.
    ///
    /// JamManager can live on the loop because it only touches memory. This
    /// cannot, and putting it there was a mistake.
    /// </summary>
    public static void Start()
    {
        lock (_workerSync)
        {
            if (_worker != null)
                return;
            _worker = new Thread(Loop)
            {
                IsBackground = true,
                Name = "SupportRotation"
            };
            _worker.Start();
        }
    }

    private static void Loop()
    {
        while (true)
        {
            // A second between passes, cut short when something has just been
            // queued so a player is not left waiting on the clock.
            _wake.Wait(1000);
            _wake.Reset();
            try
            {
                ExpireOffers();
                OfferWaiting();
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
        }
    }

    /// <summary>
    /// Offers that nobody took. The thread goes back to waiting and the staff
    /// member it was held for takes a miss; two in a row and they are switched
    /// off, because somebody who is not answering is not available whatever
    /// their toggle says.
    ///
    /// The player's thread is untouched. Under one byline there is nothing to
    /// show them - no "your chat was reassigned", no name changing in the
    /// header - which is the whole reason the reassignment can be silent.
    /// </summary>
    private static void ExpireOffers()
    {
        var now = Now();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var lapsed = connection.Query<ThreadRow>(
            "SELECT `id` AS Id, `staff_id` AS StaffId FROM `rp_support_threads` " +
            "WHERE `status` = 'offered' AND `offered_until` <= @now LIMIT 25",
            new { now }).ToList();
        if (lapsed.Count == 0)
            return;
        foreach (var thread in lapsed)
        {
            connection.Execute(
                "UPDATE `rp_support_threads` SET `status` = 'waiting', `staff_id` = 0, `offered_until` = 0 WHERE `id` = @id",
                new { id = thread.Id });
            if (thread.StaffId <= 0)
                continue;
            connection.Execute(
                "UPDATE `rp_support_rotation` SET `missed` = `missed` + 1, " +
                "`available` = IF(`missed` + 1 >= @limit, 0, `available`), `updated_at` = @now " +
                "WHERE `user_id` = @staffId",
                new { staffId = thread.StaffId, limit = MissesBeforeAway, now });
        }
    }

    /// <summary>
    /// Offer the oldest waiting threads, one staff member each.
    ///
    /// Eligibility, all of it: available, under their open-chat cap, not the
    /// player who opened the thread, and not already holding an offer. The
    /// last one matters more than it looks - without it the same staff member
    /// is offered every waiting thread at once and the rotation collapses to
    /// whoever is least busy.
    /// </summary>
    private static void OfferWaiting()
    {
        var now = Now();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var waiting = connection.Query<ThreadRow>(
            "SELECT `id` AS Id, `player_id` AS PlayerId FROM `rp_support_threads` " +
            "WHERE `status` = 'waiting' ORDER BY `created_at` ASC LIMIT 10",
            null).ToList();
        if (waiting.Count == 0)
            return;

        foreach (var thread in waiting)
        {
            // Oldest offer first: that IS the turn order, re-formed from
            // whoever is present rather than read off a stored cursor.
            var next = connection.Query<RotationRow>(
                "SELECT r.`user_id` AS UserId, r.`available` AS Available, r.`last_offered_at` AS LastOfferedAt, r.`missed` AS Missed " +
                "FROM `rp_support_rotation` r " +
                "WHERE r.`available` = 1 " +
                "  AND r.`user_id` <> @playerId " +
                "  AND (SELECT COUNT(*) FROM `rp_support_threads` t " +
                "       WHERE t.`staff_id` = r.`user_id` AND t.`status` IN ('open','offered')) < @cap " +
                "ORDER BY r.`last_offered_at` ASC, r.`user_id` ASC LIMIT 1",
                new { playerId = thread.PlayerId, cap = OpenChatCap }).FirstOrDefault();
            if (next == null)
                return; // nobody eligible; the rest of the queue waits too

            connection.Execute(
                "UPDATE `rp_support_threads` SET `status` = 'offered', `staff_id` = @staffId, " +
                "`offered_until` = @until, `offers` = `offers` + 1, `updated_at` = @now WHERE `id` = @id AND `status` = 'waiting'",
                new { id = thread.Id, staffId = next.UserId, until = now + OfferSeconds, now });
            // The pointer advances on the OFFER. A staff member who is handed a
            // chat goes to the back whether or not they take it, so a long
            // conversation never holds up the people behind it.
            connection.Execute(
                "UPDATE `rp_support_rotation` SET `last_offered_at` = @now, `updated_at` = @now WHERE `user_id` = @staffId",
                new { staffId = next.UserId, now });
        }
    }

    /// <summary>
    /// A staff member takes a chat. Only the one it was offered to, unless
    /// they are picking up something nobody holds ("Take next").
    /// Answering clears their missed count.
    /// </summary>
    public static bool Claim(int threadId, int staffId)
    {
        if (threadId <= 0 || staffId <= 0)
            return false;
        var now = Now();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var taken = connection.Execute(
            "UPDATE `rp_support_threads` SET `status` = 'open', `staff_id` = @staffId, `offered_until` = 0, `updated_at` = @now " +
            "WHERE `id` = @threadId AND (`status` = 'waiting' OR (`status` = 'offered' AND `staff_id` = @staffId))",
            new { threadId, staffId, now });
        if (taken == 0)
            return false;
        connection.Execute(
            "UPDATE `rp_support_rotation` SET `missed` = 0, `updated_at` = @now WHERE `user_id` = @staffId",
            new { staffId, now });
        return true;
    }

    /// <summary>Close a chat. Anybody on staff may close one, not only its owner.</summary>
    public static bool Resolve(int threadId, int staffId)
    {
        if (threadId <= 0 || staffId <= 0)
            return false;
        var now = Now();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Execute(
            "UPDATE `rp_support_threads` SET `status` = 'resolved', `resolved_at` = @now, `updated_at` = @now " +
            "WHERE `id` = @threadId AND `status` <> 'resolved'",
            new { threadId, now }) > 0;
    }

    /// <summary>The staff queue: everything not yet resolved, newest activity first.</summary>
    public static List<ThreadRow> StaffQueue()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<ThreadRow>(
            "SELECT t.`id` AS Id, t.`player_id` AS PlayerId, t.`category` AS Category, t.`status` AS Status, " +
            "t.`staff_id` AS StaffId, t.`offered_until` AS OfferedUntil, t.`offers` AS Offers, " +
            "t.`created_at` AS CreatedAt, t.`updated_at` AS UpdatedAt, " +
            "COALESCE(p.`username`, '') AS PlayerName, COALESCE(s.`username`, '') AS StaffName " +
            "FROM `rp_support_threads` t " +
            "LEFT JOIN `users` p ON p.`id` = t.`player_id` " +
            "LEFT JOIN `users` s ON s.`id` = t.`staff_id` " +
            "WHERE t.`status` <> 'resolved' ORDER BY t.`created_at` ASC LIMIT 60",
            null).ToList();
    }

    /// <summary>
    /// The last line of each of these threads, for a list preview. One query
    /// rather than one per thread.
    /// </summary>
    public static Dictionary<int, MessageRow> LastMessages(List<int> threadIds)
    {
        var result = new Dictionary<int, MessageRow>();
        if (threadIds == null || threadIds.Count == 0)
            return result;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var rows = connection.Query<MessageRow>(
            "SELECT m.`id` AS Id, m.`thread_id` AS ThreadId, m.`author_id` AS AuthorId, " +
            "m.`from_staff` AS FromStaff, m.`body` AS Body, m.`created_at` AS CreatedAt " +
            "FROM `rp_support_messages` m " +
            "JOIN (SELECT `thread_id`, MAX(`id`) AS `top` FROM `rp_support_messages` " +
            "      WHERE `thread_id` IN @ids GROUP BY `thread_id`) l " +
            "  ON l.`thread_id` = m.`thread_id` AND l.`top` = m.`id`",
            new { ids = threadIds });
        foreach (var row in rows)
            result[row.ThreadId] = row;
        return result;
    }

    /// <summary>How many staff are taking chats right now - the player's "Trina is online".</summary>
    public static int AvailableCount()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM `rp_support_rotation` WHERE `available` = 1");
    }

    // ---- pushing the view --------------------------------------------------

    /// <summary>
    /// Send one viewer their own half of the app: the staff queue when they
    /// are staff, the player's Trina view when they are not.
    ///
    /// WHICH COMPOSER IS DECIDED HERE AND NOWHERE ELSE, so a player can never
    /// be sent the queue by some other path forgetting to check. The player
    /// composer has no field for a staff name to leak into.
    /// </summary>
    public static void SendView(GameClient? session, int openThreadId = 0)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;

        if (IsStaff(habbo))
        {
            var queue = StaffQueue();
            var messages = openThreadId > 0 ? MessagesFor(openThreadId) : new List<MessageRow>();
            session!.Send(new RpSupportQueueComposer(
                IsAvailable(habbo.Id) ? 1 : 0, AvailableCount(), habbo.Id,
                queue, LastMessages(queue.Select(t => t.Id).ToList()), openThreadId, messages));
            return;
        }

        var threads = ThreadsForPlayer(habbo.Id);
        // A player may only open their OWN thread. Checked here rather than at
        // each caller, because this is the one place that turns an id into
        // messages.
        var open = openThreadId > 0 && threads.Any(t => t.Id == openThreadId) ? openThreadId : 0;
        session!.Send(new RpSupportComposer(
            NewsUtility.GetByline(), AvailableCount(),
            threads, LastMessages(threads.Select(t => t.Id).ToList()),
            open, open > 0 ? MessagesFor(open) : new List<MessageRow>()));
    }

    /// <summary>Refresh one player, if they are online.</summary>
    public static void PushToPlayer(int playerId)
    {
        if (playerId <= 0)
            return;
        SendView(PlusEnvironment.Game.ClientManager.GetClientByUserId(playerId));
    }

    /// <summary>
    /// Refresh every staff member online. The queue is shared, so anything
    /// that changes it changes it for all of them - and the rotation is only
    /// legible if everyone is looking at the same list.
    /// </summary>
    public static void PushToStaff()
    {
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
        {
            if (client?.GetHabbo() == null || !IsStaff(client.GetHabbo()))
                continue;
            SendView(client);
        }
    }
}
