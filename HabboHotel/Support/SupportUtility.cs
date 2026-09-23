using Dapper;
using System.Collections.Concurrent;
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

    /// <summary>
    /// How long a claimed chat may sit untouched by a staff member who is no
    /// longer in the hotel before it goes back in the queue. Long enough that
    /// a reconnect keeps your conversation; short enough that a player is not
    /// left talking to an empty chair.
    /// </summary>
    public const int AbandonSeconds = 120;

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

    /// <summary>
    /// Staff who are taking chats AND are actually in the hotel.
    ///
    /// The toggle is a row in the database and rows do not log out. Counting
    /// it alone told players "Trina is online now" when the last person to
    /// switch it on left hours ago, and - worse - handed real chats to people
    /// who were not there, burning a full OfferSeconds per absent staff member
    /// before the queue moved on. The toggle stays sticky across sessions on
    /// purpose; presence is what is checked at the point of use.
    /// </summary>
    public static List<int> AvailableStaffIds()
    {
        var online = OnlineStaffIds();
        if (online.Count == 0)
            return new List<int>();

        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<int>(
            "SELECT `user_id` FROM `rp_support_rotation` WHERE `available` = 1 AND `user_id` IN @ids",
            new { ids = online.ToList() }).ToList();
    }

    /// <summary>Every staff member in the hotel, whatever their toggle says.</summary>
    public static HashSet<int> OnlineStaffIds()
    {
        var online = new HashSet<int>();
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
        {
            var habbo = client?.GetHabbo();
            if (habbo != null && IsStaff(habbo))
                online.Add(habbo.Id);
        }
        return online;
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
                // The rotation moves chats between staff on a clock, with no
                // packet to answer. Nothing pushed the result, so a chat was
                // offered to somebody whose queue did not change until they
                // reopened the app - the round-robin was running blind. Only
                // when something actually moved, or every staff member gets
                // the whole queue again every second for nothing.
                var moved = ExpireOffers();
                moved |= ReleaseAbandoned();
                moved |= OfferWaiting();
                if (moved)
                    PushToStaff();
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
    private static bool ExpireOffers()
    {
        var now = Now();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var lapsed = connection.Query<ThreadRow>(
            "SELECT `id` AS Id, `staff_id` AS StaffId FROM `rp_support_threads` " +
            "WHERE `status` = 'offered' AND `offered_until` <= @now LIMIT 25",
            new { now }).ToList();
        if (lapsed.Count == 0)
            return false;
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
        return true;
    }

    /// <summary>
    /// Put back a chat whose owner has gone.
    ///
    /// An OFFER lapses on a clock, but a CLAIM never did - so a staff member
    /// who took a chat and then closed the client left the player talking to
    /// nobody, with no timer to rescue them and no way to ask again (they are
    /// at their open-chat cap). The grace period is generous on purpose: a
    /// reconnect, a room load or a browser refresh must not hand somebody
    /// else's conversation away underneath them.
    ///
    /// The player is told nothing, because under one byline there is nothing
    /// to tell - the next person simply picks up where the last left off.
    /// </summary>
    private static bool ReleaseAbandoned()
    {
        var online = OnlineStaffIds();
        var now = Now();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var stranded = connection.Query<int>(
            "SELECT `id` FROM `rp_support_threads` " +
            "WHERE `status` = 'open' AND `staff_id` > 0 AND `updated_at` <= @cutoff" +
            (online.Count > 0 ? " AND `staff_id` NOT IN @online" : "") + " LIMIT 25",
            new { cutoff = now - AbandonSeconds, online = online.ToList() }).ToList();
        if (stranded.Count == 0)
            return false;

        connection.Execute(
            "UPDATE `rp_support_threads` SET `status` = 'waiting', `staff_id` = 0, `offered_until` = 0, `updated_at` = @now " +
            "WHERE `id` IN @ids",
            new { ids = stranded, now });
        return true;
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
    private static bool OfferWaiting()
    {
        // Presence first: nobody in the hotel means nothing to offer, and it
        // saves the queue read entirely on a quiet night.
        var eligible = AvailableStaffIds();
        if (eligible.Count == 0)
            return false;

        var now = Now();
        var moved = false;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var waiting = connection.Query<ThreadRow>(
            "SELECT `id` AS Id, `player_id` AS PlayerId FROM `rp_support_threads` " +
            "WHERE `status` = 'waiting' ORDER BY `created_at` ASC LIMIT 10",
            null).ToList();
        if (waiting.Count == 0)
            return false;

        foreach (var thread in waiting)
        {
            // Oldest offer first: that IS the turn order, re-formed from
            // whoever is present rather than read off a stored cursor.
            var next = connection.Query<RotationRow>(
                "SELECT r.`user_id` AS UserId, r.`available` AS Available, r.`last_offered_at` AS LastOfferedAt, r.`missed` AS Missed " +
                "FROM `rp_support_rotation` r " +
                "WHERE r.`user_id` IN @eligible " +
                "  AND r.`user_id` <> @playerId " +
                "  AND (SELECT COUNT(*) FROM `rp_support_threads` t " +
                "       WHERE t.`staff_id` = r.`user_id` AND t.`status` IN ('open','offered')) < @cap " +
                "ORDER BY r.`last_offered_at` ASC, r.`user_id` ASC LIMIT 1",
                new { eligible, playerId = thread.PlayerId, cap = OpenChatCap }).FirstOrDefault();
            // Nobody for THIS thread is not nobody for the next one: the one
            // free staff member may simply be the player who opened this one,
            // or be at their cap on it. Returning here left later threads
            // sitting in a queue that could have been served.
            if (next == null)
                continue;

            var taken = connection.Execute(
                "UPDATE `rp_support_threads` SET `status` = 'offered', `staff_id` = @staffId, " +
                "`offered_until` = @until, `offers` = `offers` + 1, `updated_at` = @now WHERE `id` = @id AND `status` = 'waiting'",
                new { id = thread.Id, staffId = next.UserId, until = now + OfferSeconds, now });
            if (taken == 0)
                continue;
            moved = true;
            // The pointer advances on the OFFER. A staff member who is handed a
            // chat goes to the back whether or not they take it, so a long
            // conversation never holds up the people behind it.
            connection.Execute(
                "UPDATE `rp_support_rotation` SET `last_offered_at` = @now, `updated_at` = @now WHERE `user_id` = @staffId",
                new { staffId = next.UserId, now });
        }
        return moved;
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
    public static int AvailableCount() => AvailableStaffIds().Count;

    // ---- pushing the view --------------------------------------------------

    /// <summary>
    /// Which conversation each viewer currently has open.
    ///
    /// THIS IS WHY MESSAGES USED TO VANISH MID-CHAT. A push carried "open
    /// thread 0, no messages", because the pushing side had no idea what the
    /// person on the other end was looking at - so the moment somebody
    /// replied, the recipient's open conversation was refreshed into an empty
    /// one. Both ends, every message. The view is a whole-state packet, so a
    /// refresh has to know the thread or it silently closes it.
    ///
    /// One int per person who has opened the app; the client tells us on every
    /// open and every back, and closing the list drops the entry.
    /// </summary>
    private static readonly ConcurrentDictionary<int, int> OpenThread = new();

    /// <summary>Forget what a viewer had open - they closed the app, or logged out.</summary>
    public static void Forget(int userId) => OpenThread.TryRemove(userId, out _);

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

        // The viewer just told us where they are; remember it, so anything
        // that refreshes them later lands them back on the same screen.
        if (openThreadId > 0)
            OpenThread[habbo.Id] = openThreadId;
        else
            OpenThread.TryRemove(habbo.Id, out _);

        if (IsStaff(habbo))
        {
            var available = AvailableStaffIds();
            var queue = StaffQueue();
            SendQueue(session!, habbo, queue, LastMessages(queue.Select(t => t.Id).ToList()),
                available.Contains(habbo.Id), available.Count, openThreadId);
            return;
        }

        SendPlayer(session!, habbo, AvailableCount(), openThreadId);
    }

    /// <summary>
    /// Refresh a viewer WITHOUT moving them: whatever they had open stays
    /// open. Everything that pushes because somebody else acted uses this.
    /// </summary>
    private static void PushView(GameClient? session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        var open = OpenThread.TryGetValue(habbo.Id, out var id) ? id : 0;
        if (IsStaff(habbo))
        {
            var available = AvailableStaffIds();
            var queue = StaffQueue();
            SendQueue(session!, habbo, queue, LastMessages(queue.Select(t => t.Id).ToList()),
                available.Contains(habbo.Id), available.Count, open);
            return;
        }
        SendPlayer(session!, habbo, AvailableCount(), open);
    }

    private static void SendPlayer(GameClient session, Habbo habbo, int availableStaff, int openThreadId)
    {
        var threads = ThreadsForPlayer(habbo.Id);
        // A player may only open their OWN thread. Checked here rather than at
        // each caller, because this is the one place that turns an id into
        // messages.
        var open = openThreadId > 0 && threads.Any(t => t.Id == openThreadId) ? openThreadId : 0;
        session.Send(new RpSupportComposer(
            NewsUtility.GetByline(), availableStaff,
            threads, LastMessages(threads.Select(t => t.Id).ToList()),
            open, open > 0 ? MessagesFor(open) : new List<MessageRow>()));
    }

    private static void SendQueue(GameClient session, Habbo habbo, List<ThreadRow> queue,
        Dictionary<int, MessageRow> previews, bool available, int availableStaff, int openThreadId)
    {
        session.Send(new RpSupportQueueComposer(
            available ? 1 : 0, availableStaff, habbo.Id,
            queue, previews, openThreadId,
            openThreadId > 0 ? MessagesFor(openThreadId) : new List<MessageRow>()));
    }

    /// <summary>Refresh one player, if they are online, on whatever they have open.</summary>
    public static void PushToPlayer(int playerId)
    {
        if (playerId <= 0)
            return;
        PushView(PlusEnvironment.Game.ClientManager.GetClientByUserId(playerId));
    }

    /// <summary>
    /// Refresh every staff member online. The queue is shared, so anything
    /// that changes it changes it for all of them - and the rotation is only
    /// legible if everyone is looking at the same list.
    ///
    /// The queue itself, the previews and the availability roll-up are the
    /// same for everybody, so they are read ONCE here rather than once per
    /// viewer. Only the open conversation differs. With three staff on that
    /// was nine queries per message sent; it is now three plus one each.
    /// </summary>
    public static void PushToStaff()
    {
        var staff = new List<GameClient>();
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
        {
            if (client?.GetHabbo() == null || !IsStaff(client.GetHabbo()))
                continue;
            staff.Add(client);
        }
        if (staff.Count == 0)
            return;

        var queue = StaffQueue();
        var previews = LastMessages(queue.Select(t => t.Id).ToList());
        var available = AvailableStaffIds();
        foreach (var client in staff)
        {
            var habbo = client.GetHabbo();
            if (habbo == null)
                continue;
            SendQueue(client, habbo, queue, previews, available.Contains(habbo.Id),
                available.Count, OpenThread.TryGetValue(habbo.Id, out var id) ? id : 0);
        }
    }
}
