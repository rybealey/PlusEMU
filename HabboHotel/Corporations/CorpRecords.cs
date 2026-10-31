using Dapper;
using Plus.Communication.Packets.Outgoing.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;
using Plus.Utilities;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp Corporation Panel, HR records (migration 337): leave of absence,
/// the dismissals log and the blacklist.
///
/// LOA is whole UK days. An employee asks with :loa and a rank that approves
/// decides; management can also log one straight away for someone ranked
/// below them. While an approved LOA covers today the employee can't clock in
/// (ShiftManager.StartShift), and a week it touches counts as quota met.
///
/// Dismissals are written by :fire, :quitjob and :superfire just before the
/// employee row is deleted. The blacklist is per account, so a player's other
/// characters are refused too, and :hire checks it.
/// </summary>
public static class CorpRecords
{
    public const int LoaMaxDays = 60;
    public const int LoaMaxAhead = 30;
    public const int LoaReasonMax = 128;
    public const int BlacklistReasonMax = 128;
    public const int DismissalReasonMax = 255;

    private static int Now => (int)UnixTimestamp.GetNow();
    private static DateTime Today => CorpWeek.DayOf(DateTimeOffset.UtcNow);
    private static string Date(DateTime day) => day.ToString("yyyy-MM-dd");

    private static string Clip(string text, int max)
    {
        text = (text ?? "").Trim();
        return text.Length > max ? text[..max] : text;
    }

    // "Nov 4" - LOA dates people read.
    public static string Short(DateTime day) => day.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture);

    // ---- leave of absence ------------------------------------------------

    /// <summary>The last day of the approved LOA covering today, or null.</summary>
    public static DateTime? LoaUntil(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QuerySingleOrDefault<DateTime?>(
            "SELECT `ends_on` FROM `rp_corporation_loa` WHERE `user_id` = @userId AND `status` = 'approved' " +
            "AND `starts_on` <= @today AND `ends_on` >= @today ORDER BY `ends_on` DESC LIMIT 1",
            new { userId, today = Date(Today) });
    }

    /// <summary>Who in a corporation had an approved LOA touching the week of this Monday.</summary>
    public static HashSet<int> OnLoaInWeek(int corpId, DateTime monday)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<int>(
            "SELECT DISTINCT `user_id` FROM `rp_corporation_loa` WHERE `corporation_id` = @corpId AND `status` IN ('approved','ended') " +
            "AND `starts_on` <= @sunday AND `ends_on` >= @monday",
            new { corpId, monday = Date(monday), sunday = Date(monday.AddDays(6)) }).ToHashSet();
    }

    /// <summary>On leave today, and LOA requests waiting for a decision, for the Dashboard and the sidebar.</summary>
    public static (int OnLeave, int Pending) LoaCounts(int corpId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QuerySingle<(int OnLeave, int Pending)>(
            "SELECT CAST(COALESCE(SUM(`status` = 'approved' AND `starts_on` <= @today AND `ends_on` >= @today), 0) AS SIGNED) AS OnLeave, " +
            "CAST(COALESCE(SUM(`status` = 'pending'), 0) AS SIGNED) AS Pending " +
            "FROM `rp_corporation_loa` WHERE `corporation_id` = @corpId", new { corpId, today = Date(Today) });
    }

    private static bool Overlaps(System.Data.IDbConnection connection, int userId, DateTime from, DateTime to, int exceptId = 0) =>
        connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM `rp_corporation_loa` WHERE `user_id` = @userId AND `status` IN ('pending','approved') AND `id` <> @exceptId " +
            "AND `starts_on` <= @to AND `ends_on` >= @from",
            new { userId, from = Date(from), to = Date(to), exceptId }) > 0;

    /// <summary>:loa &lt;days&gt; [reason] - ask for leave from today. :loa end - come back early.</summary>
    public static void LoaCommand(GameClient session, string[] parameters)
    {
        var userId = session.GetHabbo().Id;
        var employment = CorporationUtility.GetEmployment(userId);
        if (employment == null || employment.CorpId == 0)
        {
            session.SendWhisper("You don't work for a corporation.");
            return;
        }
        if (parameters.Length > 0 && parameters[0].Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            var ended = connection.Execute(
                "UPDATE `rp_corporation_loa` SET `status` = 'ended', `ends_on` = GREATEST(`starts_on`, @yesterday), `decided_at` = @now " +
                "WHERE `user_id` = @userId AND `status` = 'approved' AND `starts_on` <= @today AND `ends_on` >= @today",
                new { userId, yesterday = Date(Today.AddDays(-1)), today = Date(Today), now = Now });
            var withdrawn = connection.Execute(
                "UPDATE `rp_corporation_loa` SET `status` = 'cancelled', `decided_at` = @now WHERE `user_id` = @userId AND `status` = 'pending'",
                new { userId, now = Now });
            session.SendWhisper(ended > 0 ? "Welcome back. Your leave has ended and you can clock in again."
                : withdrawn > 0 ? "Your leave request is withdrawn." : "You're not on leave.");
            return;
        }
        if (parameters.Length == 0 || !int.TryParse(parameters[0], out var days) || days < 1 || days > LoaMaxDays)
        {
            session.SendWhisper($"Usage: :loa <days, 1-{LoaMaxDays}> [reason] - or :loa end to come back early.");
            return;
        }
        var reason = Clip(string.Join(" ", parameters.Skip(1)), LoaReasonMax);
        var from = Today;
        var to = from.AddDays(days - 1);
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (Overlaps(connection, userId, from, to))
            {
                session.SendWhisper("You already have leave asked for or approved over those days. :loa end withdraws it.");
                return;
            }
            connection.Execute(
                "INSERT INTO `rp_corporation_loa` (`corporation_id`, `user_id`, `starts_on`, `ends_on`, `reason`, `status`, `requested_by`, `requested_by_name`, `created_at`) " +
                "VALUES (@corpId, @userId, @from, @to, @reason, 'pending', @userId, @name, @now)",
                new { corpId = employment.CorpId, userId, from = Date(from), to = Date(to), reason, name = session.GetHabbo().Username, now = Now });
        }
        session.SendWhisper($"Leave asked for: {Short(from)} to {Short(to)}. Your management decides in the Corporation Panel.");
    }

    private sealed class LoaRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Look { get; set; } = "";
        public string Gender { get; set; } = "";
        public string RankName { get; set; } = "";
        public int Tier { get; set; }
        public int RankOrder { get; set; }
        public DateTime StartsOn { get; set; }
        public DateTime EndsOn { get; set; }
        public string Reason { get; set; } = "";
        public string Status { get; set; } = "";
        public int RequestedBy { get; set; }
        public string RequestedByName { get; set; } = "";
        public string DecidedByName { get; set; } = "";
        public int CreatedAt { get; set; }
    }

    private const string LoaSelect =
        "SELECT l.`id` AS Id, l.`user_id` AS UserId, COALESCE(u.`username`, '') AS Username, COALESCE(u.`look`, '') AS Look, COALESCE(u.`gender`, 'M') AS Gender, " +
        "COALESCE(r.`name`, '') AS RankName, COALESCE(e.`tier`, 0) AS Tier, COALESCE(r.`rank_order`, 0) AS RankOrder, l.`starts_on` AS StartsOn, l.`ends_on` AS EndsOn, " +
        "l.`reason` AS Reason, l.`status` AS Status, l.`requested_by` AS RequestedBy, l.`requested_by_name` AS RequestedByName, l.`decided_by_name` AS DecidedByName, " +
        "l.`created_at` AS CreatedAt " +
        "FROM `rp_corporation_loa` l " +
        "LEFT JOIN `users` u ON u.`id` = l.`user_id` " +
        "LEFT JOIN `rp_corporation_employees` e ON e.`user_id` = l.`user_id` AND e.`corporation_id` = l.`corporation_id` " +
        "LEFT JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` ";

    private static LoaRow FindLoa(System.Data.IDbConnection connection, int corpId, int id) =>
        connection.QuerySingleOrDefault<LoaRow>(LoaSelect + "WHERE l.`id` = @id AND l.`corporation_id` = @corpId LIMIT 1", new { id, corpId });

    /// <summary>Panel: log an LOA for someone ranked below you, approved at once.</summary>
    public static (bool Ok, string Message) LogLoa(GameClient session, CorpPanel.Viewer viewer, int userId, int startsIn, int days, string reason)
    {
        if (startsIn < 0 || startsIn > LoaMaxAhead || days < 1 || days > LoaMaxDays)
            return (false, $"Leave starts within {LoaMaxAhead} days and lasts 1 to {LoaMaxDays} days.");
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var member = CorpPanel.Employees(connection, viewer.CorpId).FirstOrDefault(person => person.UserId == userId);
        if (member == null)
            return (false, "They don't work here any more.");
        if (!CorpDiscipline.CanDiscipline(viewer, member.UserId, member.RankOrder))
            return (false, $"{member.Username} isn't ranked below you.");
        var from = Today.AddDays(startsIn);
        var to = from.AddDays(days - 1);
        if (Overlaps(connection, userId, from, to))
            return (false, $"{member.Username} already has leave asked for or approved over those days.");
        var name = session.GetHabbo().Username;
        connection.Execute(
            "INSERT INTO `rp_corporation_loa` (`corporation_id`, `user_id`, `starts_on`, `ends_on`, `reason`, `status`, `requested_by`, `requested_by_name`, `decided_by_name`, `decided_at`, `created_at`) " +
            "VALUES (@corpId, @userId, @from, @to, @reason, 'approved', @by, @name, @name, @now, @now)",
            new { corpId = viewer.CorpId, userId, from = Date(from), to = Date(to), reason = Clip(reason, LoaReasonMax), by = viewer.UserId, name, now = Now });
        // On leave from today: off the clock now.
        if (startsIn == 0)
            EndShiftForLeave(userId);
        return (true, $"Logged leave for {member.Username}: {Short(from)} to {Short(to)}.");
    }

    // An employee whose leave starts today stops working now.
    private static void EndShiftForLeave(int userId)
    {
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        if (client?.GetHabbo() != null && ShiftManager.IsOnDuty(userId))
            ShiftManager.StopShift(client);
    }

    /// <summary>Panel: approve or deny an LOA request (a rank that approves).</summary>
    public static (bool Ok, string Message) DecideLoa(GameClient session, CorpPanel.Viewer viewer, int id, bool approve)
    {
        if (viewer.CanApprove != 1)
            return (false, "Your rank can't approve leave.");
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var loa = FindLoa(connection, viewer.CorpId, id);
        if (loa == null || loa.Status != "pending")
            return (false, "That request has already been decided.");
        if (loa.UserId == viewer.UserId)
            return (false, "Someone else has to decide your own leave.");
        var decided = connection.Execute(
            "UPDATE `rp_corporation_loa` SET `status` = @status, `decided_by_name` = @name, `decided_at` = @now WHERE `id` = @id AND `status` = 'pending'",
            new { status = approve ? "approved" : "denied", name = session.GetHabbo().Username, now = Now, id });
        if (decided == 0)
            return (false, "That request has already been decided.");

        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(loa.UserId);
        if (approve && loa.StartsOn <= Today)
            EndShiftForLeave(loa.UserId);
        client?.SendNotification(approve
            ? $"Your leave from {Short(loa.StartsOn)} to {Short(loa.EndsOn)} is approved. You can't clock in until it ends; :loa end brings you back early."
            : $"Your leave from {Short(loa.StartsOn)} to {Short(loa.EndsOn)} wasn't approved.");
        return (true, approve ? $"Approved {loa.Username}'s leave." : $"Denied {loa.Username}'s leave.");
    }

    /// <summary>Panel: end an approved LOA early (or call off one that hasn't started).</summary>
    public static (bool Ok, string Message) EndLoa(GameClient session, CorpPanel.Viewer viewer, int id)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var loa = FindLoa(connection, viewer.CorpId, id);
        if (loa == null || loa.Status != "approved" || loa.EndsOn < Today)
            return (false, "That leave isn't running.");
        if (viewer.CanApprove != 1 && !CorpDiscipline.CanDiscipline(viewer, loa.UserId, loa.RankOrder))
            return (false, $"{loa.Username} isn't ranked below you.");
        var notStarted = loa.StartsOn > Today;
        connection.Execute(
            "UPDATE `rp_corporation_loa` SET `status` = @status, `ends_on` = GREATEST(`starts_on`, @yesterday), `decided_at` = @now WHERE `id` = @id",
            new { status = notStarted ? "cancelled" : "ended", yesterday = Date(Today.AddDays(-1)), now = Now, id });
        return (true, notStarted ? $"Called off {loa.Username}'s leave." : $"Ended {loa.Username}'s leave. They can clock in again.");
    }

    public static RpCorpPanelLoaComposer BuildLoa(CorpPanel.Viewer viewer)
    {
        var monday = CorpWeek.WeekStart();
        List<LoaRow> rows;
        List<CorpPanel.Person> people;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            // Pending ones, and every approved or ended one still in the
            // three weeks shown (from the week before this one).
            rows = connection.Query<LoaRow>(LoaSelect +
                "WHERE l.`corporation_id` = @corpId AND (l.`status` = 'pending' OR (l.`status` IN ('approved','ended') AND l.`ends_on` >= @from)) " +
                "ORDER BY l.`starts_on`, l.`id`", new { corpId = viewer.CorpId, from = Date(monday.AddDays(-7)) }).ToList();
            people = CorpPanel.Employees(connection, viewer.CorpId);
        }
        var entries = rows.Select(row => new RpCorpPanelLoaComposer.Entry(row.Id, row.UserId, row.Username, row.Look, row.Gender,
            CorpRanks.Title(row.RankName, row.Tier), Date(row.StartsOn), Date(row.EndsOn), row.Reason, row.Status, row.RequestedByName,
            row.RequestedBy == row.UserId, row.DecidedByName, row.CreatedAt)).ToList();
        var targets = people.Select(person => new RpCorpPanelLoaComposer.Person(person.UserId, person.Username,
            CorpRanks.Title(person.RankName, person.Tier), CorpDiscipline.CanDiscipline(viewer, person.UserId, person.RankOrder))).ToList();
        return new RpCorpPanelLoaComposer(viewer.CanApprove == 1, Date(Today), Date(monday), entries, targets);
    }

    // ---- dismissals ---------------------------------------------------------

    /// <summary>
    /// Log someone leaving a corporation - call it BEFORE the employee row is
    /// deleted, while their rank is still there to read. Also withdraws their
    /// open leave and rank requests, which can't be decided any more.
    /// </summary>
    public static void LogLeaving(int userId, string kind, string reason, int actorId, string actorName)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var row = connection.QuerySingleOrDefault<(int CorpId, string Username, string RankName, int Tier, int HiredAt)?>(
            "SELECT e.`corporation_id` AS CorpId, u.`username` AS Username, r.`name` AS RankName, e.`tier` AS Tier, e.`hired_at` AS HiredAt " +
            "FROM `rp_corporation_employees` e INNER JOIN `users` u ON u.`id` = e.`user_id` INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`user_id` = @userId LIMIT 1", new { userId });
        if (row == null)
            return;
        connection.Execute(
            "INSERT INTO `rp_corporation_dismissals` (`corporation_id`, `user_id`, `username`, `rank_name`, `tier`, `kind`, `reason`, `actor_id`, `actor_name`, `hired_at`, `created_at`) " +
            "VALUES (@corpId, @userId, @username, @rankName, @tier, @kind, @reason, @actorId, @actorName, @hiredAt, @now)",
            new
            {
                corpId = row.Value.CorpId, userId, username = row.Value.Username, rankName = row.Value.RankName, tier = row.Value.Tier, kind,
                reason = Clip(reason, DismissalReasonMax), actorId, actorName = actorName ?? "", hiredAt = row.Value.HiredAt, now = Now
            });
        connection.Execute(
            "UPDATE `rp_corporation_loa` SET `status` = IF(`status` = 'pending', 'cancelled', 'ended'), `ends_on` = IF(`ends_on` >= @today, GREATEST(`starts_on`, @yesterday), `ends_on`), `decided_at` = @now " +
            "WHERE `user_id` = @userId AND `corporation_id` = @corpId AND (`status` = 'pending' OR (`status` = 'approved' AND `ends_on` >= @today))",
            new { userId, corpId = row.Value.CorpId, today = Date(Today), yesterday = Date(Today.AddDays(-1)), now = Now });
        connection.Execute(
            "UPDATE `rp_corporation_rank_requests` SET `status` = 'cancelled', `decided_by_name` = '', `decided_at` = @now " +
            "WHERE `user_id` = @userId AND `corporation_id` = @corpId AND `status` = 'pending'",
            new { userId, corpId = row.Value.CorpId, now = Now });
    }

    private sealed class DismissalRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Look { get; set; } = "";
        public string Gender { get; set; } = "";
        public string RankName { get; set; } = "";
        public int Tier { get; set; }
        public string Kind { get; set; } = "";
        public string Reason { get; set; } = "";
        public string ActorName { get; set; } = "";
        public int HiredAt { get; set; }
        public int CreatedAt { get; set; }
    }

    /// <summary>Panel: the corporation's leavers in the last <paramref name="days"/> days (0 = all, newest 200).</summary>
    public static RpCorpPanelDismissalsComposer BuildDismissals(CorpPanel.Viewer viewer, int days)
    {
        days = days switch { 30 => 30, 90 => 90, 0 => 0, _ => 30 };
        List<DismissalRow> rows;
        Dictionary<int, (int ExpiresAt, string Reason)> listed;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            rows = connection.Query<DismissalRow>(
                "SELECT d.`id` AS Id, d.`user_id` AS UserId, d.`username` AS Username, COALESCE(u.`look`, '') AS Look, COALESCE(u.`gender`, 'M') AS Gender, " +
                "d.`rank_name` AS RankName, d.`tier` AS Tier, d.`kind` AS Kind, d.`reason` AS Reason, d.`actor_name` AS ActorName, d.`hired_at` AS HiredAt, d.`created_at` AS CreatedAt " +
                "FROM `rp_corporation_dismissals` d LEFT JOIN `users` u ON u.`id` = d.`user_id` " +
                "WHERE d.`corporation_id` = @corpId AND (@days = 0 OR d.`created_at` >= @since) ORDER BY d.`created_at` DESC, d.`id` DESC LIMIT 200",
                new { corpId = viewer.CorpId, days, since = Now - days * 86400 }).ToList();
            listed = ActiveByAccount(connection, viewer.CorpId);
        }
        var result = rows.Select(row =>
        {
            var account = AccountUtility.RootOf(row.UserId);
            var on = listed.TryGetValue(account, out var entry);
            return new RpCorpPanelDismissalsComposer.Row(row.Id, row.UserId, row.Username, row.Look, row.Gender, CorpRanks.Title(row.RankName, row.Tier),
                row.Kind, row.Reason, row.ActorName, row.HiredAt, row.CreatedAt,
                !on ? "eligible" : entry.ExpiresAt == 0 ? "never" : "until", on ? entry.ExpiresAt : 0);
        }).ToList();
        return new RpCorpPanelDismissalsComposer(days, result);
    }

    // ---- blacklist ----------------------------------------------------------

    // Live entries of a corporation by account.
    private static Dictionary<int, (int ExpiresAt, string Reason)> ActiveByAccount(System.Data.IDbConnection connection, int corpId) =>
        connection.Query<(int AccountId, int ExpiresAt, string Reason)>(
                "SELECT `account_id` AS AccountId, `expires_at` AS ExpiresAt, `reason` AS Reason FROM `rp_corporation_blacklist` " +
                "WHERE `corporation_id` = @corpId AND `removed_at` = 0 AND (`expires_at` = 0 OR `expires_at` > @now)",
                new { corpId, now = Now })
            .GroupBy(row => row.AccountId)
            // a permanent one outranks any end date; else the latest end
            .ToDictionary(group => group.Key, group =>
            {
                var best = group.OrderBy(row => row.ExpiresAt == 0 ? 0 : 1).ThenByDescending(row => row.ExpiresAt).First();
                return (best.ExpiresAt, best.Reason);
            });

    /// <summary>
    /// Why this player (any of their characters) can't be hired by the
    /// corporation, or null. :hire asks this.
    /// </summary>
    public static string BlacklistRefusal(int corpId, int userId, string username)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        if (!ActiveByAccount(connection, corpId).TryGetValue(AccountUtility.RootOf(userId), out var entry))
            return null;
        var until = entry.ExpiresAt == 0 ? "for good" : $"until {Short(DateTimeOffset.FromUnixTimeSeconds(entry.ExpiresAt).UtcDateTime)}";
        return $"{username} is on your corporation's blacklist {until}" + (entry.Reason.Length > 0 ? $": {entry.Reason}." : ".");
    }

    /// <summary>Panel: add a player to the blacklist for <paramref name="days"/> days (0 = permanent).</summary>
    public static (bool Ok, string Message) AddToBlacklist(GameClient session, CorpPanel.Viewer viewer, string username, string reason, int days)
    {
        if (days < 0 || days > 365)
            return (false, "Pick how long: up to a year, or permanent.");
        reason = Clip(reason, BlacklistReasonMax);
        if (reason.Length == 0)
            return (false, "Say why. Whoever checks the list later will read it.");
        var target = CorporationUtility.ResolveUser((username ?? "").Trim());
        if (target == null)
            return (false, $"Nobody is called {username}.");
        var account = AccountUtility.RootOf(target.Id);
        if (account == AccountUtility.RootOf(viewer.UserId))
            return (false, "You can't blacklist yourself.");
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var employee = CorpPanel.Employees(connection, viewer.CorpId).FirstOrDefault(person => AccountUtility.RootOf(person.UserId) == account);
        if (employee != null && employee.RankOrder >= viewer.RankOrder)
            return (false, $"{employee.Username} works here at or above your rank.");
        if (ActiveByAccount(connection, viewer.CorpId).ContainsKey(account))
            return (false, $"{target.Username} is already on the blacklist. Remove that entry first to change it.");
        connection.Execute(
            "INSERT INTO `rp_corporation_blacklist` (`corporation_id`, `account_id`, `user_id`, `username`, `reason`, `added_by_name`, `created_at`, `expires_at`) " +
            "VALUES (@corpId, @account, @userId, @username, @reason, @name, @now, @expires)",
            new { corpId = viewer.CorpId, account, userId = target.Id, username = target.Username, reason, name = session.GetHabbo().Username, now = Now,
                expires = days == 0 ? 0 : Now + days * 86400 });
        var still = employee != null ? $" {employee.Username} still works here: use :fire if they should go." : "";
        return (true, $"Added {target.Username} to the blacklist {(days == 0 ? "for good" : $"for {days} days")}.{still}");
    }

    /// <summary>Panel: take someone off the blacklist - whoever added them, or a rank that approves.</summary>
    public static (bool Ok, string Message) RemoveFromBlacklist(GameClient session, CorpPanel.Viewer viewer, int id)
    {
        var name = session.GetHabbo().Username;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var entry = connection.QuerySingleOrDefault<(string Username, string AddedByName)?>(
            "SELECT `username` AS Username, `added_by_name` AS AddedByName FROM `rp_corporation_blacklist` " +
            "WHERE `id` = @id AND `corporation_id` = @corpId AND `removed_at` = 0 LIMIT 1", new { id, corpId = viewer.CorpId });
        if (entry == null)
            return (false, "That entry is already gone.");
        if (viewer.CanApprove != 1 && !string.Equals(entry.Value.AddedByName, name, StringComparison.OrdinalIgnoreCase))
            return (false, $"Only {entry.Value.AddedByName} or a rank that approves can remove that.");
        connection.Execute("UPDATE `rp_corporation_blacklist` SET `removed_by_name` = @name, `removed_at` = @now WHERE `id` = @id",
            new { name, now = Now, id });
        return (true, $"Took {entry.Value.Username} off the blacklist.");
    }

    /// <summary>Panel: "Before you hire" - is this player (any of their characters) on the list?</summary>
    public static (bool Ok, string Message) CheckBlacklist(CorpPanel.Viewer viewer, string username)
    {
        var target = CorporationUtility.ResolveUser((username ?? "").Trim());
        if (target == null)
            return (false, $"Nobody is called {username}.");
        var refusal = BlacklistRefusal(viewer.CorpId, target.Id, target.Username);
        return refusal == null ? (true, $"{target.Username} is clear to hire.") : (false, refusal);
    }

    private sealed class BlacklistRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Look { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Reason { get; set; } = "";
        public string AddedByName { get; set; } = "";
        public int CreatedAt { get; set; }
        public int ExpiresAt { get; set; }
    }

    public static RpCorpPanelBlacklistComposer BuildBlacklist(GameClient session, CorpPanel.Viewer viewer)
    {
        List<BlacklistRow> rows;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            rows = connection.Query<BlacklistRow>(
                "SELECT b.`id` AS Id, b.`user_id` AS UserId, b.`username` AS Username, COALESCE(u.`look`, '') AS Look, COALESCE(u.`gender`, 'M') AS Gender, " +
                "b.`reason` AS Reason, b.`added_by_name` AS AddedByName, b.`created_at` AS CreatedAt, b.`expires_at` AS ExpiresAt " +
                "FROM `rp_corporation_blacklist` b LEFT JOIN `users` u ON u.`id` = b.`user_id` " +
                "WHERE b.`corporation_id` = @corpId AND b.`removed_at` = 0 AND (b.`expires_at` = 0 OR b.`expires_at` > @now) " +
                "ORDER BY b.`created_at` DESC, b.`id` DESC", new { corpId = viewer.CorpId, now = Now }).ToList();
        var name = session.GetHabbo().Username;
        var entries = rows.Select(row => new RpCorpPanelBlacklistComposer.Entry(row.Id, row.UserId, row.Username, row.Look, row.Gender, row.Reason,
            row.AddedByName, row.CreatedAt, row.ExpiresAt,
            viewer.CanApprove == 1 || string.Equals(row.AddedByName, name, StringComparison.OrdinalIgnoreCase))).ToList();
        return new RpCorpPanelBlacklistComposer(entries);
    }
}
