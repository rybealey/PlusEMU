using Dapper;
using Plus.Communication.Packets.Outgoing.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: the Corporation Panel (:cp) - a corporation's own management
/// window, for the ranks flagged panel_access (migration 334). Every action
/// arrives as RpCorpPanelActionEvent and is checked here against the sender's
/// CURRENT employment, so a demotion or firing takes the panel away at once.
/// It only ever shows the sender's own corporation.
/// </summary>
public static class CorpPanel
{
    /// <summary>What the client may offer; the server checks each action again.</summary>
    [Flags]
    public enum Capability
    {
        None = 0,
        View = 1,
        // can_approve: decide promotion and demotion requests
        Approve = 2,
        // at or above manage_rank_order: what :hire and :fire allow
        Manage = 4
    }

    /// <summary>Panel actions (the first int of RpCorpPanelActionEvent).</summary>
    public enum Action
    {
        Open = 0,
        Dashboard = 1,
        Shifts = 2,
        // the week's warnings and strikes, with everyone's counts
        Discipline = 3,
        // one employee's file; the int is their user id
        Employee = 4
    }

    /// <summary>Discipline actions (the first int of RpCorpPanelDisciplineEvent).</summary>
    public enum DisciplineAction
    {
        Issue = 0,
        Revoke = 1
    }

    // Three weeks back is what the panel's week switch offers.
    public const int WeeksShown = 3;

    public sealed class Viewer
    {
        public int UserId { get; set; }
        public int CorpId { get; set; }
        public string CorpName { get; set; } = "";
        public string Badge { get; set; } = "";
        public int RankId { get; set; }
        public int RankOrder { get; set; }
        public string RankName { get; set; } = "";
        public int PanelAccess { get; set; }
        public int CanApprove { get; set; }
        public int ManageRankOrder { get; set; }
        public int ShiftQuota { get; set; }
    }

    // TINYINT(1) flags compared in SQL so they arrive as integers (a bool or
    // enum string would not convert the same way on every column).
    private static Viewer Resolve(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QuerySingleOrDefault<Viewer>(
            "SELECT e.`user_id` AS UserId, c.`id` AS CorpId, c.`name` AS CorpName, c.`badge` AS Badge, " +
            "r.`id` AS RankId, r.`rank_order` AS RankOrder, r.`name` AS RankName, " +
            "(r.`panel_access` = 1) AS PanelAccess, (r.`can_approve` = 1) AS CanApprove, " +
            "c.`manage_rank_order` AS ManageRankOrder, c.`shift_quota` AS ShiftQuota " +
            "FROM `rp_corporation_employees` e " +
            "INNER JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`user_id` = @userId LIMIT 1", new { userId });
    }

    /// <summary>
    /// The sender's employment if their rank opens the panel, else null -
    /// with the reason whispered when <paramref name="whisper"/> (the :cp
    /// command; packets fail silently, the window simply stays as it was).
    /// </summary>
    public static Viewer Require(GameClient session, bool whisper)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return null;
        var viewer = Resolve(habbo.Id);
        if (viewer == null)
        {
            if (whisper) session.SendWhisper("You don't work for a corporation.");
            return null;
        }
        if (viewer.PanelAccess != 1)
        {
            if (whisper) session.SendWhisper("Only your corporation's management can open the Corporation Panel.");
            return null;
        }
        return viewer;
    }

    public static Capability CapabilitiesOf(Viewer viewer)
    {
        var caps = Capability.View;
        if (viewer.CanApprove == 1) caps |= Capability.Approve;
        if (viewer.RankOrder >= viewer.ManageRankOrder) caps |= Capability.Manage;
        return caps;
    }

    public static void Handle(GameClient session, int action, int arg)
    {
        var viewer = Require(session, whisper: false);
        if (viewer == null)
        {
            // Lost access with the window open (fired, demoted): close it.
            session?.Send(new Communication.Packets.Outgoing.Avatar.InClientLinkComposer("corp-panel/hide"));
            return;
        }
        var weeksAgo = Math.Clamp(arg, 0, WeeksShown - 1);
        switch ((Action)action)
        {
            case Action.Open:
                session.Send(BuildContext(viewer));
                return;
            case Action.Dashboard:
                session.Send(BuildDashboard(viewer, weeksAgo));
                return;
            case Action.Shifts:
                session.Send(BuildShifts(viewer, weeksAgo));
                return;
            case Action.Discipline:
            {
                List<Person> people;
                using (var connection = PlusEnvironment.DatabaseManager.Connection())
                    people = Employees(connection, viewer.CorpId);
                session.Send(CorpDiscipline.BuildList(viewer, weeksAgo, people));
                return;
            }
            case Action.Employee:
            {
                var file = BuildEmployee(viewer, arg);
                if (file != null)
                    session.Send(file);
                return;
            }
        }
    }

    /// <summary>
    /// Issue (0: target user id) or revoke (1: entry id) a warning or strike.
    /// Always answered with RpCorpPanelResultComposer, which the form shows.
    /// </summary>
    public static void HandleDiscipline(GameClient session, int action, int id, string kind, string reason, string details, bool notify)
    {
        var viewer = Require(session, whisper: false);
        if (viewer == null)
        {
            session?.Send(new Communication.Packets.Outgoing.Avatar.InClientLinkComposer("corp-panel/hide"));
            return;
        }
        var result = (DisciplineAction)action switch
        {
            DisciplineAction.Issue => CorpDiscipline.Issue(session, viewer, id, kind, reason, details, notify),
            DisciplineAction.Revoke => CorpDiscipline.Revoke(session, viewer, id),
            _ => (false, "Unknown action.")
        };
        session.Send(new RpCorpPanelResultComposer(action, result.Item1, result.Item2));
    }

    private sealed class FileRow
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Look { get; set; } = "";
        public string Gender { get; set; } = "";
        public string RankName { get; set; } = "";
        public int RankOrder { get; set; }
        public int Tier { get; set; }
        public int HiredAt { get; set; }
        public int LastOnline { get; set; }
        public int ShiftSecondsWeek { get; set; }
    }

    // How many weeks of shifts the employee file draws.
    public const int FileWeeks = 6;

    /// <summary>One employee's file - only for someone in the viewer's own corporation.</summary>
    public static RpCorpPanelEmployeeComposer BuildEmployee(Viewer viewer, int userId)
    {
        FileRow row;
        List<(DateTime Day, int Seconds)> days;
        var oldest = CorpWeek.WeekStart(FileWeeks - 1);
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            row = connection.QuerySingleOrDefault<FileRow>(
                "SELECT e.`user_id` AS UserId, u.`username` AS Username, u.`look` AS Look, u.`gender` AS Gender, r.`name` AS RankName, " +
                "r.`rank_order` AS RankOrder, e.`tier` AS Tier, e.`hired_at` AS HiredAt, COALESCE(u.`last_online`, 0) AS LastOnline, " +
                "e.`shift_seconds_week` AS ShiftSecondsWeek " +
                "FROM `rp_corporation_employees` e " +
                "INNER JOIN `users` u ON u.`id` = e.`user_id` " +
                "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
                "WHERE e.`user_id` = @userId AND e.`corporation_id` = @corpId LIMIT 1", new { userId, corpId = viewer.CorpId });
            if (row == null)
                return null;
            days = connection.Query<(DateTime Day, int Seconds)>(
                "SELECT `day` AS Day, `seconds` AS Seconds FROM `rp_corporation_shift_days` " +
                "WHERE `user_id` = @userId AND `corporation_id` = @corpId AND `day` >= @from",
                new { userId, corpId = viewer.CorpId, from = oldest.ToString("yyyy-MM-dd") }).ToList();
        }
        // Oldest week first; this week reads the live counter, as the grid does.
        var weeks = new int[FileWeeks];
        foreach (var day in days)
        {
            var index = (int)((day.Day.Date - oldest).TotalDays / 7);
            if (index >= 0 && index < FileWeeks - 1)
                weeks[index] += day.Seconds;
        }
        weeks[FileWeeks - 1] = row.ShiftSecondsWeek + ShiftManager.LiveSessionSeconds(row.UserId);
        var counts = CorpDiscipline.CountsForWeek(viewer.CorpId, 0);
        counts.TryGetValue(row.UserId, out var count);
        return new RpCorpPanelEmployeeComposer(row.UserId, row.Username, row.Look, row.Gender, row.RankName, row.Tier, row.HiredAt, row.LastOnline,
            ShiftManager.IsOnDuty(row.UserId), IsOnline(row.UserId), viewer.ShiftQuota, count.Warnings, count.Strikes,
            CorpDiscipline.CanDiscipline(viewer, row.UserId, row.RankOrder), CorpWeek.StartedAt(oldest), weeks,
            CorpDiscipline.ForEmployee(viewer.CorpId, row.UserId));
    }

    public static RpCorpPanelComposer BuildContext(Viewer viewer)
    {
        var monday = CorpWeek.WeekStart();
        return new RpCorpPanelComposer(viewer.CorpId, viewer.CorpName, viewer.Badge, viewer.RankName,
            (int)CapabilitiesOf(viewer), viewer.ShiftQuota, CorpWeek.StartedAt(monday), CorpWeek.ResetAt(monday));
    }

    public sealed class Person
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Look { get; set; } = "";
        public string Gender { get; set; } = "";
        public string RankName { get; set; } = "";
        public int RankOrder { get; set; }
        public int Tier { get; set; }
        public int Tiers { get; set; }
        public int PanelAccess { get; set; }
        public int ShiftSecondsWeek { get; set; }
    }

    public static List<Person> Employees(System.Data.IDbConnection connection, int corpId) =>
        connection.Query<Person>(
            "SELECT e.`user_id` AS UserId, u.`username` AS Username, u.`look` AS Look, u.`gender` AS Gender, " +
            "r.`name` AS RankName, r.`rank_order` AS RankOrder, e.`tier` AS Tier, r.`tiers` AS Tiers, " +
            "(r.`panel_access` = 1) AS PanelAccess, e.`shift_seconds_week` AS ShiftSecondsWeek " +
            "FROM `rp_corporation_employees` e " +
            "INNER JOIN `users` u ON u.`id` = e.`user_id` " +
            "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`corporation_id` = @corpId " +
            "ORDER BY r.`rank_order` DESC, e.`tier` DESC, u.`username`", new { corpId }).ToList();

    // Seconds per employee per day (index 0 = Monday) for one week.
    private static Dictionary<int, int[]> Days(System.Data.IDbConnection connection, int corpId, DateTime monday)
    {
        var rows = connection.Query<(int UserId, DateTime Day, int Seconds)>(
            "SELECT `user_id` AS UserId, `day` AS Day, `seconds` AS Seconds FROM `rp_corporation_shift_days` " +
            "WHERE `corporation_id` = @corpId AND `day` BETWEEN @from AND @to",
            new { corpId, from = monday.ToString("yyyy-MM-dd"), to = monday.AddDays(6).ToString("yyyy-MM-dd") });
        var days = new Dictionary<int, int[]>();
        foreach (var row in rows)
        {
            var index = (int)(row.Day.Date - monday).TotalDays;
            if (index < 0 || index > 6)
                continue;
            if (!days.TryGetValue(row.UserId, out var week))
                days[row.UserId] = week = new int[7];
            week[index] += row.Seconds;
        }
        return days;
    }

    // The week's total for one employee. This week reads the live counter
    // (it includes a shift still running, and the days before the panel
    // existed); past weeks add up their days.
    private static int WeekSeconds(Person employee, int[] days, int weeksAgo) =>
        weeksAgo == 0
            ? employee.ShiftSecondsWeek + ShiftManager.LiveSessionSeconds(employee.UserId)
            : (days?.Sum() ?? 0);

    private static bool IsOnline(int userId) => PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.GetHabbo() != null;

    public static RpCorpPanelShiftsComposer BuildShifts(Viewer viewer, int weeksAgo)
    {
        var monday = CorpWeek.WeekStart(weeksAgo);
        List<Person> employees;
        Dictionary<int, int[]> days;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            employees = Employees(connection, viewer.CorpId);
            days = Days(connection, viewer.CorpId, monday);
        }
        var rows = employees.Select(employee =>
        {
            days.TryGetValue(employee.UserId, out var week);
            return new RpCorpPanelShiftsComposer.Row(employee.UserId, employee.Username, employee.Look, employee.Gender,
                employee.RankName, employee.RankOrder, employee.Tier, employee.Tiers,
                ShiftManager.IsOnDuty(employee.UserId), IsOnline(employee.UserId),
                WeekSeconds(employee, week, weeksAgo), week ?? new int[7]);
        }).ToList();
        return new RpCorpPanelShiftsComposer(weeksAgo, CorpWeek.StartedAt(monday), CorpWeek.ResetAt(monday), viewer.ShiftQuota, rows);
    }

    public static RpCorpPanelDashboardComposer BuildDashboard(Viewer viewer, int weeksAgo)
    {
        var monday = CorpWeek.WeekStart(weeksAgo);
        List<Person> employees;
        Dictionary<int, int[]> days;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            employees = Employees(connection, viewer.CorpId);
            days = Days(connection, viewer.CorpId, monday);
        }
        var quotaSeconds = viewer.ShiftQuota * ShiftManager.PayIntervalSeconds;
        var dayTotals = new int[7];
        foreach (var week in days.Values)
            for (var i = 0; i < 7; i++)
                dayTotals[i] += week[i];
        var totals = employees.ToDictionary(employee => employee.UserId, employee =>
        {
            days.TryGetValue(employee.UserId, out var week);
            return WeekSeconds(employee, week, weeksAgo);
        });
        var leaders = employees
            .Where(employee => employee.PanelAccess == 1)
            .Select(employee => new RpCorpPanelDashboardComposer.Leader(employee.UserId, employee.Username, employee.Look, employee.Gender,
                employee.RankName, ShiftManager.IsOnDuty(employee.UserId), IsOnline(employee.UserId), totals[employee.UserId]))
            .ToList();
        var discipline = CorpDiscipline.CountsForWeek(viewer.CorpId, weeksAgo);
        return new RpCorpPanelDashboardComposer(weeksAgo, CorpWeek.StartedAt(monday), CorpWeek.ResetAt(monday),
            employees.Count,
            employees.Count(employee => ShiftManager.IsOnDuty(employee.UserId)),
            totals.Values.Sum(),
            quotaSeconds > 0 ? totals.Values.Count(seconds => seconds >= quotaSeconds) : 0,
            viewer.ShiftQuota, dayTotals, leaders,
            discipline.Values.Sum(count => count.Warnings),
            discipline.Values.Sum(count => count.Strikes),
            discipline.Values.Count(count => count.Strikes >= CorpDiscipline.StrikesForReview));
    }
}
