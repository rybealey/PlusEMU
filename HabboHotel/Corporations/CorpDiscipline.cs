using Dapper;
using Plus.Communication.Packets.Outgoing.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.Utilities;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp Corporation Panel: warnings and strikes (rp_corporation_discipline,
/// migration 335). Both belong to a corporation week and roll over with it:
/// three warnings in one week add a strike by themselves, and three strikes in
/// one week put the employee up for a dismissal review - a list for
/// management to act on, never an automatic firing.
///
/// Who may: anyone the panel opens for, on employees ranked below them in
/// their own corporation (the :fire rule). Revoking is for whoever issued it
/// or a rank that approves. Revoked rows stay, marked, for the employee file.
/// </summary>
public static class CorpDiscipline
{
    public const string Warning = "warning";
    public const string Strike = "strike";

    // per week: this many warnings make a strike, this many strikes a review
    public const int WarningsPerStrike = 3;
    public const int StrikesForReview = 3;

    public const int ReasonMax = 64;
    public const int DetailsMax = 255;

    public sealed class Entry
    {
        public int Id { get; set; }
        public string Kind { get; set; } = "";
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Look { get; set; } = "";
        public string Gender { get; set; } = "";
        public string RankName { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Details { get; set; } = "";
        public int IssuedBy { get; set; }
        public string IssuedByName { get; set; } = "";
        public int Automatic { get; set; }
        public int CreatedAt { get; set; }
        public string RevokedByName { get; set; } = "";
        public int RevokedAt { get; set; }
    }

    private const string EntrySelect =
        "SELECT d.`id` AS Id, d.`kind` AS Kind, d.`user_id` AS UserId, COALESCE(u.`username`, '') AS Username, " +
        "COALESCE(u.`look`, '') AS Look, COALESCE(u.`gender`, 'M') AS Gender, COALESCE(r.`name`, '') AS RankName, " +
        "d.`reason` AS Reason, d.`details` AS Details, d.`issued_by` AS IssuedBy, d.`issued_by_name` AS IssuedByName, " +
        "(d.`automatic` = 1) AS Automatic, d.`created_at` AS CreatedAt, d.`revoked_by_name` AS RevokedByName, d.`revoked_at` AS RevokedAt " +
        "FROM `rp_corporation_discipline` d " +
        "LEFT JOIN `users` u ON u.`id` = d.`user_id` " +
        "LEFT JOIN `rp_corporation_employees` e ON e.`user_id` = d.`user_id` AND e.`corporation_id` = d.`corporation_id` " +
        "LEFT JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` ";

    private static string Week(int weeksAgo = 0) => CorpWeek.WeekStart(weeksAgo).ToString("yyyy-MM-dd");

    /// <summary>A week's warnings and strikes for one corporation, revoked ones left out, newest first.</summary>
    public static List<Entry> ForWeek(int corpId, int weeksAgo)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<Entry>(EntrySelect +
            "WHERE d.`corporation_id` = @corpId AND d.`week_start` = @week AND d.`revoked_at` = 0 ORDER BY d.`created_at` DESC, d.`id` DESC",
            new { corpId, week = Week(weeksAgo) }).ToList();
    }

    /// <summary>One employee's whole record in a corporation, revoked ones included, newest first.</summary>
    public static List<Entry> ForEmployee(int corpId, int userId, int limit = 50)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<Entry>(EntrySelect +
            "WHERE d.`corporation_id` = @corpId AND d.`user_id` = @userId ORDER BY d.`created_at` DESC, d.`id` DESC LIMIT @limit",
            new { corpId, userId, limit }).ToList();
    }

    /// <summary>Live warnings and strikes per employee for one week of one corporation.</summary>
    public static Dictionary<int, (int Warnings, int Strikes)> CountsForWeek(int corpId, int weeksAgo)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<(int UserId, int Warnings, int Strikes)>(
                "SELECT `user_id` AS UserId, CAST(SUM(`kind` = 'warning') AS SIGNED) AS Warnings, CAST(SUM(`kind` = 'strike') AS SIGNED) AS Strikes " +
                "FROM `rp_corporation_discipline` WHERE `corporation_id` = @corpId AND `week_start` = @week AND `revoked_at` = 0 GROUP BY `user_id`",
                new { corpId, week = Week(weeksAgo) })
            .ToDictionary(row => row.UserId, row => (row.Warnings, row.Strikes));
    }

    private sealed class Target
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public int RankOrder { get; set; }
    }

    // The employee, if they work for this corporation right now.
    private static Target FindTarget(int corpId, int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QuerySingleOrDefault<Target>(
            "SELECT e.`user_id` AS UserId, u.`username` AS Username, r.`rank_order` AS RankOrder " +
            "FROM `rp_corporation_employees` e " +
            "INNER JOIN `users` u ON u.`id` = e.`user_id` " +
            "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`user_id` = @userId AND e.`corporation_id` = @corpId LIMIT 1", new { userId, corpId });
    }

    /// <summary>May this viewer discipline an employee of this rank? The :fire rule: strictly below them.</summary>
    public static bool CanDiscipline(CorpPanel.Viewer viewer, int targetUserId, int targetRankOrder) =>
        targetUserId != viewer.UserId && targetRankOrder < viewer.RankOrder;

    private static int Insert(System.Data.IDbConnection connection, int corpId, int userId, string kind, string reason, string details,
        int issuedBy, string issuedByName, bool automatic) =>
        connection.ExecuteScalar<int>(
            "INSERT INTO `rp_corporation_discipline` (`corporation_id`, `user_id`, `kind`, `reason`, `details`, `issued_by`, `issued_by_name`, `automatic`, `week_start`, `created_at`) " +
            "VALUES (@corpId, @userId, @kind, @reason, @details, @issuedBy, @issuedByName, @automatic, @week, @now); SELECT LAST_INSERT_ID();",
            new { corpId, userId, kind, reason, details, issuedBy, issuedByName, automatic = automatic ? 1 : 0, week = Week(), now = (int)UnixTimestamp.GetNow() });

    /// <summary>
    /// Issue a warning or strike. Returns the message for the form: what
    /// happened, or why not. A warning that is the third this week also adds
    /// the strike, and the message says so.
    /// </summary>
    public static (bool Ok, string Message) Issue(GameClient session, CorpPanel.Viewer viewer, int targetUserId, string kind, string reason, string details, bool notify)
    {
        if (kind != Warning && kind != Strike)
            return (false, "That isn't something you can issue.");
        reason = (reason ?? "").Trim();
        details = (details ?? "").Trim();
        if (reason.Length == 0)
            return (false, "Pick or write a reason.");
        if (reason.Length > ReasonMax)
            reason = reason[..ReasonMax];
        if (details.Length > DetailsMax)
            details = details[..DetailsMax];

        var target = FindTarget(viewer.CorpId, targetUserId);
        if (target == null)
            return (false, "They don't work here any more.");
        if (!CanDiscipline(viewer, target.UserId, target.RankOrder))
            return (false, target.UserId == viewer.UserId ? "You can't discipline yourself." : $"{target.Username} isn't ranked below you.");

        var issuer = session.GetHabbo().Username;
        var addedStrike = false;
        var strikes = 0;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            Insert(connection, viewer.CorpId, target.UserId, kind, reason, details, viewer.UserId, issuer, automatic: false);
            var counts = connection.QuerySingle<(int Warnings, int Strikes)>(
                "SELECT CAST(COALESCE(SUM(`kind` = 'warning'), 0) AS SIGNED) AS Warnings, CAST(COALESCE(SUM(`kind` = 'strike'), 0) AS SIGNED) AS Strikes " +
                "FROM `rp_corporation_discipline` WHERE `corporation_id` = @corpId AND `user_id` = @userId AND `week_start` = @week AND `revoked_at` = 0",
                new { corpId = viewer.CorpId, userId = target.UserId, week = Week() });
            strikes = counts.Strikes;
            if (kind == Warning && counts.Warnings > 0 && counts.Warnings % WarningsPerStrike == 0)
            {
                Insert(connection, viewer.CorpId, target.UserId, Strike, $"{WarningsPerStrike} warnings in one week", reason, viewer.UserId, issuer, automatic: true);
                addedStrike = true;
                strikes++;
            }
        }

        if (notify)
            Tell(target.UserId, viewer.CorpName, kind, reason, details, addedStrike);

        var what = kind == Warning ? "Warning" : "Strike";
        var message = $"{what} issued to {target.Username}.";
        if (addedStrike)
            message += $" It was their {Ordinal(WarningsPerStrike)} warning this week, so a strike was added too.";
        if ((kind == Strike || addedStrike) && strikes >= StrikesForReview)
            message += $" That's {strikes} strikes this week: they're up for a dismissal review.";
        return (true, message);
    }

    /// <summary>Revoke a live warning or strike of the viewer's corporation.</summary>
    public static (bool Ok, string Message) Revoke(GameClient session, CorpPanel.Viewer viewer, int entryId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var entry = connection.QuerySingleOrDefault<Entry>(EntrySelect + "WHERE d.`id` = @entryId AND d.`corporation_id` = @corpId LIMIT 1",
            new { entryId, corpId = viewer.CorpId });
        if (entry == null || entry.RevokedAt != 0)
            return (false, "That's already gone.");
        if (entry.IssuedBy != viewer.UserId && viewer.CanApprove != 1)
            return (false, $"Only {entry.IssuedByName} or a manager who approves requests can revoke that.");
        connection.Execute(
            "UPDATE `rp_corporation_discipline` SET `revoked_by_name` = @name, `revoked_at` = @now WHERE `id` = @entryId AND `revoked_at` = 0",
            new { name = session.GetHabbo().Username, now = (int)UnixTimestamp.GetNow(), entryId });
        return (true, $"{(entry.Kind == Warning ? "Warning" : "Strike")} revoked for {entry.Username}.");
    }

    // An alert for the employee, if they're online. The record is in their
    // file either way.
    private static void Tell(int userId, string corpName, string kind, string reason, string details, bool addedStrike)
    {
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        if (client?.GetHabbo() == null)
            return;
        var text = $"You've been given a {kind} at {corpName}: {reason}.";
        if (details.Length > 0)
            text += $"\n\n\"{details}\"";
        if (addedStrike)
            text += $"\n\nThat's your {Ordinal(WarningsPerStrike)} warning this week, so it adds a strike.";
        text += "\n\nWarnings and strikes clear when the week resets, Sunday 11:59 PM UK.";
        client.SendNotification(text);
    }

    private static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };

    public static RpCorpPanelDisciplineComposer BuildList(CorpPanel.Viewer viewer, int weeksAgo, List<CorpPanel.Person> people)
    {
        var monday = CorpWeek.WeekStart(weeksAgo);
        var counts = CountsForWeek(viewer.CorpId, weeksAgo);
        var rows = people.Select(person =>
        {
            counts.TryGetValue(person.UserId, out var count);
            return new RpCorpPanelDisciplineComposer.Person(person.UserId, person.Username, person.Look, person.Gender, person.RankName, person.Tier,
                count.Warnings, count.Strikes, CanDiscipline(viewer, person.UserId, person.RankOrder));
        }).ToList();
        return new RpCorpPanelDisciplineComposer(weeksAgo, CorpWeek.StartedAt(monday), CorpWeek.ResetAt(monday),
            WarningsPerStrike, StrikesForReview, ForWeek(viewer.CorpId, weeksAgo), rows);
    }
}
