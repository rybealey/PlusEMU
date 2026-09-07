using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.Utilities;

namespace Plus.HabboHotel.Notifications;

/// <summary>
/// pixelrp: the phone's notifications. Anything that happens to a player
/// while they are online is pushed to their client the moment it happens,
/// and the phone turns it into a banner, a row in the Notification Center
/// and a number on the app's badge.
///
/// There is no state here and no table: the client keeps its own list (see
/// RpNotificationComposer). This is only the fan-out - who to tell, and the
/// one thing the server has to notice on its own, which is an event that is
/// about to start.
///
/// Whether a player WANTS a given notification is a phone setting, kept with
/// the rest of the phone's preferences on the client. So everything is
/// pushed and the phone decides what to show; nothing here reads a
/// preference.
/// </summary>
public static class NotificationUtility
{
    // apps
    public const string Photos = "photos";
    public const string Contacts = "contacts";
    public const string Calendar = "calendar";
    public const string Notes = "notes";
    public const string News = "news";

    /// <summary>How long before an event starts the reminder goes out.</summary>
    private const int ReminderLeadSeconds = 600;

    /// <summary>How often the reminder sweep runs. Comfortably inside the lead
    /// time, so a reminder is never missed and never more than this late.</summary>
    private const int ReminderTickMs = 30000;

    /// <summary>
    /// Reminders already sent, keyed "eventId:startsAt" so that moving an
    /// event re-arms its reminder - a player told about 21:00 should hear
    /// again when it becomes 22:00. Entries drop out once the event has
    /// started.
    /// </summary>
    private static readonly HashSet<string> Reminded = new();

    private static System.Threading.Timer _timer;

    public static void Init() => _timer = new System.Threading.Timer(_ => ReminderTick(), null, ReminderTickMs, ReminderTickMs);

    // ---- fan-out ----------------------------------------------------------

    /// <summary>Tell one player, if they are online.</summary>
    public static void Push(int userId, string app, string kind, string subject, string actor = "", int targetId = 0, int extra = 0, bool transient = false)
    {
        if (userId <= 0) return;
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        if (client?.GetHabbo() == null) return;
        client.Send(new RpNotificationComposer(app, kind, subject, actor, targetId, extra, transient));
    }

    /// <summary>Tell several players. Duplicates and the actor are dropped, so
    /// callers can pass "everyone involved" without filtering themselves out.</summary>
    public static void PushTo(IEnumerable<int> userIds, string app, string kind, string subject, string actor = "", int targetId = 0, int extra = 0, bool transient = false, int exceptUserId = 0)
    {
        if (userIds == null) return;
        foreach (var userId in userIds.Distinct())
        {
            if (userId == exceptUserId) continue;
            Push(userId, app, kind, subject, actor, targetId, extra, transient);
        }
    }

    /// <summary>Tell the whole hotel - staff calendar and news changes.</summary>
    public static void PushAll(string app, string kind, string subject, string actor = "", int targetId = 0, int extra = 0, bool transient = false, int exceptUserId = 0)
    {
        var packet = new RpNotificationComposer(app, kind, subject, actor, targetId, extra, transient);
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
        {
            var habbo = client?.GetHabbo();
            if (habbo == null || habbo.Id == exceptUserId) continue;
            client.Send(packet);
        }
    }

    // ---- quiet periods ----------------------------------------------------

    /// <summary>Last time a debounced key fired.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> LastFired = new();

    /// <summary>
    /// True the first time a key comes up, then false again until the window
    /// has passed. For notifications whose trigger repeats far faster than
    /// anyone wants telling - a live note editor saves as you type, and one
    /// "Bella edited it" is the whole message however many saves that was.
    /// </summary>
    public static bool Debounce(string key, int windowSeconds)
    {
        var now = DateTime.UtcNow;
        if (LastFired.TryGetValue(key, out var last) && (now - last).TotalSeconds < windowSeconds)
            return false;
        LastFired[key] = now;

        // Keys are per note/album/player, so the map is small - but it must
        // not grow forever on a long-running server.
        if (LastFired.Count > 512)
            foreach (var (stale, at) in LastFired.ToList())
                if ((now - at).TotalSeconds > windowSeconds)
                    LastFired.TryRemove(stale, out _);

        return true;
    }

    // ---- the one thing nobody tells us about ------------------------------

    /// <summary>
    /// Sweeps for events starting within the lead time and reminds the hotel
    /// once each. All-day events are skipped - they have no start to be ten
    /// minutes away from.
    /// </summary>
    private static void ReminderTick()
    {
        try
        {
            var now = (int)UnixTimestamp.GetNow();
            List<UpcomingRow> upcoming;
            using (var connection = PlusEnvironment.DatabaseManager.Connection())
                upcoming = connection.Query<UpcomingRow>(
                    "SELECT `id` AS Id, `title` AS Title, `starts_at` AS StartsAt FROM `rp_events` " +
                    "WHERE `all_day` = 0 AND `starts_at` >= @now ORDER BY `starts_at`", new { now }).ToList();

            lock (Reminded)
            {
                foreach (var row in upcoming)
                {
                    if (row.StartsAt - now > ReminderLeadSeconds) continue;
                    var key = $"{row.Id}:{row.StartsAt}";
                    if (!Reminded.Add(key)) continue;
                    PushAll(Calendar, "event_soon", row.Title, "", row.Id);
                }

                // Anything no longer upcoming has either started or been
                // deleted; either way its reminder can be forgotten.
                var live = new HashSet<string>(upcoming.Select(row => $"{row.Id}:{row.StartsAt}"));
                Reminded.RemoveWhere(key => !live.Contains(key));
            }
        }
        catch
        {
            // A reminder sweep is best-effort: a database hiccup must not take
            // the timer (or the emulator) down with it.
        }
    }

    private class UpcomingRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int StartsAt { get; set; }
    }
}
