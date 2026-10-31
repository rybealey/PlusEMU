using Dapper;
using Plus.Communication.Packets.Outgoing.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.Utilities;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp Corporation Panel: promotions and demotions (migration 336).
///
/// A corporation's ladder is every rank lowest first, each split into its
/// tiers (I..tiers) or one step for an untiered leadership rank. A promotion
/// or demotion is one step on it. Whoever makes it - :promote / :demote, or a
/// request approved in the panel - it lands in rp_corporation_rank_changes by
/// itself, with the employee's week at the time.
///
/// The rules, for :promote / :demote and for approving: a rank with
/// can_approve, on someone ranked below them, never to their own rank or
/// above, never themselves. Anyone the panel opens for may request the same
/// step; someone else who approves decides.
/// </summary>
public static class CorpRanks
{
    public const string Promotion = "promotion";
    public const string Demotion = "demotion";

    public const int ReasonMax = 255;
    public const int NoteMax = 128;

    private static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };

    public static string Title(string rankName, int tier) =>
        tier < 1 ? rankName ?? "" : $"{rankName} {Numerals[Math.Min(tier, Numerals.Length) - 1]}";

    public sealed record Step(int RankId, int RankOrder, string RankName, int Tier)
    {
        public string Title => CorpRanks.Title(RankName, Tier);
    }

    // Lowest first.
    public static List<Step> Ladder(System.Data.IDbConnection connection, int corpId)
    {
        var ranks = connection.Query<(int Id, int RankOrder, string Name, int Tiers)>(
            "SELECT `id` AS Id, `rank_order` AS RankOrder, `name` AS Name, `tiers` AS Tiers FROM `rp_corporation_ranks` " +
            "WHERE `corporation_id` = @corpId ORDER BY `rank_order`", new { corpId });
        var steps = new List<Step>();
        foreach (var rank in ranks)
        {
            if (rank.Tiers <= 0)
                steps.Add(new Step(rank.Id, rank.RankOrder, rank.Name, 0));
            else
                for (var tier = 1; tier <= Math.Min(rank.Tiers, Numerals.Length); tier++)
                    steps.Add(new Step(rank.Id, rank.RankOrder, rank.Name, tier));
        }
        return steps;
    }

    public sealed class Member
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Look { get; set; } = "";
        public string Gender { get; set; } = "";
        public int RankId { get; set; }
        public int RankOrder { get; set; }
        public string RankName { get; set; } = "";
        public int Tier { get; set; }
        public int HiredAt { get; set; }
        public int ShiftSecondsWeek { get; set; }
    }

    private const string MemberSelect =
        "SELECT e.`user_id` AS UserId, u.`username` AS Username, u.`look` AS Look, u.`gender` AS Gender, e.`rank_id` AS RankId, " +
        "r.`rank_order` AS RankOrder, r.`name` AS RankName, e.`tier` AS Tier, e.`hired_at` AS HiredAt, e.`shift_seconds_week` AS ShiftSecondsWeek " +
        "FROM `rp_corporation_employees` e " +
        "INNER JOIN `users` u ON u.`id` = e.`user_id` " +
        "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` ";

    private static Member FindMember(System.Data.IDbConnection connection, int corpId, int userId) =>
        connection.QuerySingleOrDefault<Member>(MemberSelect + "WHERE e.`user_id` = @userId AND e.`corporation_id` = @corpId LIMIT 1",
            new { userId, corpId });

    private static List<Member> Members(System.Data.IDbConnection connection, int corpId) =>
        connection.Query<Member>(MemberSelect + "WHERE e.`corporation_id` = @corpId ORDER BY r.`rank_order` DESC, e.`tier` DESC, u.`username`",
            new { corpId }).ToList();

    // Where on the ladder a member stands; -1 if their rank/tier isn't on it
    // (a tier above the rank's count, after a staff edit) - then the nearest
    // step of their rank counts.
    private static int IndexOf(List<Step> ladder, Member member)
    {
        var exact = ladder.FindIndex(step => step.RankId == member.RankId && step.Tier == member.Tier);
        if (exact >= 0)
            return exact;
        return ladder.FindLastIndex(step => step.RankId == member.RankId);
    }

    /// <summary>The step one up or down from where the member stands, or null at either end.</summary>
    public static Step Next(List<Step> ladder, Member member, string direction)
    {
        var index = IndexOf(ladder, member);
        if (index < 0)
            return null;
        var next = direction == Promotion ? index + 1 : index - 1;
        return next >= 0 && next < ladder.Count ? ladder[next] : null;
    }

    /// <summary>
    /// Why this actor (rank order, user id) may not move the member this way,
    /// or null when they may. The same rule for doing it, requesting it and
    /// approving it.
    /// </summary>
    private static string Refusal(int actorUserId, int actorRankOrder, Member member, Step to, string direction)
    {
        if (member.UserId == actorUserId)
            return "You can't change your own rank.";
        if (member.RankOrder >= actorRankOrder)
            return $"{member.Username} isn't ranked below you.";
        if (to == null)
            return direction == Promotion
                ? $"{member.Username} is already at the top of the ladder."
                : $"{member.Username} is already at the lowest rank. Use :fire if they should go.";
        if (direction == Promotion && to.RankOrder >= actorRankOrder)
            return $"You can only promote up to the rank below yours; {to.Title} isn't.";
        return null;
    }

    private static string Week() => CorpWeek.WeekStart().ToString("yyyy-MM-dd");

    /// <summary>
    /// Move the member one step and log it. The update only lands if they're
    /// still where we read them, so two managers clicking at once move one step.
    /// </summary>
    private static (bool Ok, string Message, Step To) Apply(System.Data.IDbConnection connection, int actorId, string actorName, int actorRankOrder,
        int corpId, string corpName, int userId, string direction, string reason, string source, string requestedByName)
    {
        var member = FindMember(connection, corpId, userId);
        if (member == null)
            return (false, "They don't work here any more.", null);
        var ladder = Ladder(connection, corpId);
        var to = Next(ladder, member, direction);
        var refusal = Refusal(actorId, actorRankOrder, member, to, direction);
        if (refusal != null)
            return (false, refusal, null);

        var moved = connection.Execute(
            "UPDATE `rp_corporation_employees` SET `rank_id` = @toRank, `tier` = @toTier " +
            "WHERE `user_id` = @userId AND `corporation_id` = @corpId AND `rank_id` = @fromRank AND `tier` = @fromTier LIMIT 1",
            new { toRank = to.RankId, toTier = to.Tier, userId, corpId, fromRank = member.RankId, fromTier = member.Tier });
        if (moved == 0)
            return (false, $"{member.Username}'s rank changed a moment ago. Look again and retry.", null);

        var counts = CorpDiscipline.CountsForWeek(corpId, 0);
        counts.TryGetValue(userId, out var count);
        connection.Execute(
            "INSERT INTO `rp_corporation_rank_changes` (`corporation_id`, `user_id`, `username`, `direction`, `from_rank_name`, `from_tier`, `to_rank_name`, `to_tier`, " +
            "`reason`, `actor_id`, `actor_name`, `source`, `requested_by_name`, `shift_seconds_week`, `warnings`, `strikes`, `week_start`, `created_at`) " +
            "VALUES (@corpId, @userId, @username, @direction, @fromName, @fromTier, @toName, @toTier, @reason, @actorId, @actorName, @source, @requestedByName, " +
            "@week_seconds, @warnings, @strikes, @week, @now)",
            new
            {
                corpId, userId, username = member.Username, direction, fromName = member.RankName, fromTier = member.Tier, toName = to.RankName, toTier = to.Tier,
                reason = Clip(reason, ReasonMax), actorId, actorName, source, requestedByName = requestedByName ?? "",
                week_seconds = member.ShiftSecondsWeek + ShiftManager.LiveSessionSeconds(userId), warnings = count.Warnings, strikes = count.Strikes,
                week = Week(), now = (int)UnixTimestamp.GetNow()
            });

        // Infostands, profiles and an on-duty wage, motto and uniform follow.
        CorporationUtility.BroadcastEmployment(userId);

        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        if (client?.GetHabbo() != null)
        {
            var text = direction == Promotion
                ? $"You've been promoted to {to.Title} at {corpName}."
                : $"You've been demoted to {to.Title} at {corpName}.";
            if (!string.IsNullOrWhiteSpace(reason))
                text += $"\n\n\"{Clip(reason, ReasonMax)}\"";
            client.SendNotification(text);
        }

        var verb = direction == Promotion ? "Promoted" : "Demoted";
        return (true, $"{verb} {member.Username} to {to.Title}.", to);
    }

    private static string Clip(string text, int max)
    {
        text = (text ?? "").Trim();
        return text.Length > max ? text[..max] : text;
    }

    /// <summary>:promote / :demote - straight away, for a rank that approves.</summary>
    public static void Command(GameClient session, string username, string direction, string reason)
    {
        var viewer = CorpPanel.Require(session, whisper: false);
        if (viewer == null || viewer.CanApprove != 1)
        {
            session.SendWhisper(viewer == null && CorporationUtility.GetEmployment(session.GetHabbo().Id) == null
                ? "You don't work for a corporation."
                : "Only ranks that approve promotions can do that. Request it in :cp instead.");
            return;
        }
        var target = CorporationUtility.ResolveUser(username);
        if (target == null)
        {
            session.SendWhisper($"Nobody is called {username}.");
            return;
        }
        (bool Ok, string Message, Step To) result;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            result = Apply(connection, viewer.UserId, session.GetHabbo().Username, viewer.RankOrder, viewer.CorpId, viewer.CorpName, target.Id,
                direction, reason, "command", "");
        if (!result.Ok)
        {
            session.SendWhisper(result.Message);
            return;
        }
        // Public, as :hire is: the room sees it.
        var actor = session.GetHabbo().CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(session.GetHabbo().Id);
        var verb = direction == Promotion ? "promotes" : "demotes";
        if (actor != null)
            actor.OnChat(23, $"*{verb} {target.Username} to {result.To.Title}*", true);
        else
            session.SendWhisper(result.Message);
    }

    /// <summary>Panel: ask for a promotion or demotion one step from where they stand.</summary>
    public static (bool Ok, string Message) Request(GameClient session, CorpPanel.Viewer viewer, int userId, string direction, string reason)
    {
        if (direction != Promotion && direction != Demotion)
            return (false, "That isn't a request you can make.");
        reason = Clip(reason, ReasonMax);
        if (reason.Length == 0)
            return (false, "Say why - whoever approves it will read it.");
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var member = FindMember(connection, viewer.CorpId, userId);
        if (member == null)
            return (false, "They don't work here any more.");
        var to = Next(Ladder(connection, viewer.CorpId), member, direction);
        var refusal = Refusal(viewer.UserId, viewer.RankOrder, member, to, direction);
        if (refusal != null)
            return (false, refusal);
        var open = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM `rp_corporation_rank_requests` WHERE `corporation_id` = @corpId AND `user_id` = @userId AND `status` = 'pending'",
            new { corpId = viewer.CorpId, userId });
        if (open > 0)
            return (false, $"There's already an open request for {member.Username}. Decide that one first.");
        connection.Execute(
            "INSERT INTO `rp_corporation_rank_requests` (`corporation_id`, `user_id`, `direction`, `reason`, `requested_by`, `requested_by_name`, `created_at`) " +
            "VALUES (@corpId, @userId, @direction, @reason, @by, @byName, @now)",
            new { corpId = viewer.CorpId, userId, direction, reason, by = viewer.UserId, byName = session.GetHabbo().Username, now = (int)UnixTimestamp.GetNow() });
        var what = direction == Promotion ? "promote" : "demote";
        return (true, $"Asked to {what} {member.Username} to {to.Title}. Management can approve it in :cp.");
    }

    private sealed class RequestRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Look { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Reason { get; set; } = "";
        public int RequestedBy { get; set; }
        public string RequestedByName { get; set; } = "";
        public string Status { get; set; } = "";
        public string DecidedByName { get; set; } = "";
        public int DecidedAt { get; set; }
        public string Note { get; set; } = "";
        public int CreatedAt { get; set; }
    }

    private const string RequestSelect =
        "SELECT q.`id` AS Id, q.`user_id` AS UserId, COALESCE(u.`username`, '') AS Username, COALESCE(u.`look`, '') AS Look, COALESCE(u.`gender`, 'M') AS Gender, " +
        "q.`direction` AS Direction, q.`reason` AS Reason, q.`requested_by` AS RequestedBy, q.`requested_by_name` AS RequestedByName, q.`status` AS Status, " +
        "q.`decided_by_name` AS DecidedByName, q.`decided_at` AS DecidedAt, q.`note` AS Note, q.`created_at` AS CreatedAt " +
        "FROM `rp_corporation_rank_requests` q LEFT JOIN `users` u ON u.`id` = q.`user_id` ";

    /// <summary>Panel: approve or deny an open request - a rank that approves, not the one who asked.</summary>
    public static (bool Ok, string Message) Decide(GameClient session, CorpPanel.Viewer viewer, int requestId, bool approve, string note)
    {
        if (viewer.CanApprove != 1)
            return (false, "Your rank can't approve requests.");
        note = Clip(note, NoteMax);
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var request = connection.QuerySingleOrDefault<RequestRow>(RequestSelect + "WHERE q.`id` = @requestId AND q.`corporation_id` = @corpId LIMIT 1",
            new { requestId, corpId = viewer.CorpId });
        if (request == null || request.Status != "pending")
            return (false, "That request has already been decided.");
        if (request.RequestedBy == viewer.UserId)
            return (false, "You asked for this one, so someone else has to decide it.");

        // Claim it first: two managers approving at once must not move the
        // employee two steps. An approval that then can't be carried out
        // (the rule refuses it now) goes back to pending.
        var name = session.GetHabbo().Username;
        var claimed = connection.Execute(
            "UPDATE `rp_corporation_rank_requests` SET `status` = @status, `decided_by_name` = @name, `decided_at` = @now, `note` = @note " +
            "WHERE `id` = @requestId AND `status` = 'pending'",
            new { status = approve ? "approved" : "denied", name, now = (int)UnixTimestamp.GetNow(), note, requestId });
        if (claimed == 0)
            return (false, "That request has already been decided.");
        if (!approve)
            return (true, $"Denied the request for {request.Username}.");

        var result = Apply(connection, viewer.UserId, name, viewer.RankOrder, viewer.CorpId, viewer.CorpName, request.UserId, request.Direction,
            request.Reason, "request", request.RequestedByName);
        if (!result.Ok)
        {
            connection.Execute(
                "UPDATE `rp_corporation_rank_requests` SET `status` = 'pending', `decided_by_name` = '', `decided_at` = 0, `note` = '' WHERE `id` = @requestId",
                new { requestId });
            return (false, result.Message);
        }
        return (true, $"{(request.Direction == Promotion ? "Promoted" : "Demoted")} {request.Username} to {result.To.Title}. It's in the log.");
    }

    /// <summary>Panel: withdraw your own open request.</summary>
    public static (bool Ok, string Message) Cancel(GameClient session, CorpPanel.Viewer viewer, int requestId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var cancelled = connection.Execute(
            "UPDATE `rp_corporation_rank_requests` SET `status` = 'cancelled', `decided_by_name` = @name, `decided_at` = @now " +
            "WHERE `id` = @requestId AND `corporation_id` = @corpId AND `requested_by` = @userId AND `status` = 'pending'",
            new { name = session.GetHabbo().Username, now = (int)UnixTimestamp.GetNow(), requestId, corpId = viewer.CorpId, userId = viewer.UserId });
        return cancelled > 0 ? (true, "Request withdrawn.") : (false, "Only an open request of your own can be withdrawn.");
    }

    /// <summary>Open requests by direction, for the sidebar and the Dashboard.</summary>
    public static (int Promotions, int Demotions) PendingCounts(int corpId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QuerySingle<(int Promotions, int Demotions)>(
            "SELECT CAST(COALESCE(SUM(`direction` = 'promotion'), 0) AS SIGNED) AS Promotions, CAST(COALESCE(SUM(`direction` = 'demotion'), 0) AS SIGNED) AS Demotions " +
            "FROM `rp_corporation_rank_requests` WHERE `corporation_id` = @corpId AND `status` = 'pending'", new { corpId });
    }

    public sealed class Change
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Look { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Direction { get; set; } = "";
        public string FromRankName { get; set; } = "";
        public int FromTier { get; set; }
        public string ToRankName { get; set; } = "";
        public int ToTier { get; set; }
        public string Reason { get; set; } = "";
        public string ActorName { get; set; } = "";
        public string Source { get; set; } = "";
        public string RequestedByName { get; set; } = "";
        public int ShiftSecondsWeek { get; set; }
        public int Warnings { get; set; }
        public int Strikes { get; set; }
        public int CreatedAt { get; set; }
    }

    private const string ChangeSelect =
        "SELECT c.`id` AS Id, c.`user_id` AS UserId, c.`username` AS Username, COALESCE(u.`look`, '') AS Look, COALESCE(u.`gender`, 'M') AS Gender, " +
        "c.`direction` AS Direction, c.`from_rank_name` AS FromRankName, c.`from_tier` AS FromTier, c.`to_rank_name` AS ToRankName, c.`to_tier` AS ToTier, " +
        "c.`reason` AS Reason, c.`actor_name` AS ActorName, c.`source` AS Source, c.`requested_by_name` AS RequestedByName, " +
        "c.`shift_seconds_week` AS ShiftSecondsWeek, c.`warnings` AS Warnings, c.`strikes` AS Strikes, c.`created_at` AS CreatedAt " +
        "FROM `rp_corporation_rank_changes` c LEFT JOIN `users` u ON u.`id` = c.`user_id` ";

    /// <summary>One employee's rank changes in a corporation, newest first, for their file.</summary>
    public static List<Change> ForEmployee(int corpId, int userId, int limit = 50)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<Change>(ChangeSelect +
            "WHERE c.`corporation_id` = @corpId AND c.`user_id` = @userId ORDER BY c.`created_at` DESC, c.`id` DESC LIMIT @limit",
            new { corpId, userId, limit }).ToList();
    }

    public static RpCorpPanelRankLogComposer BuildLog(CorpPanel.Viewer viewer, int weeksAgo)
    {
        var monday = CorpWeek.WeekStart(weeksAgo);
        List<Change> changes;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            changes = connection.Query<Change>(ChangeSelect +
                "WHERE c.`corporation_id` = @corpId AND c.`week_start` = @week ORDER BY c.`created_at` DESC, c.`id` DESC",
                new { corpId = viewer.CorpId, week = monday.ToString("yyyy-MM-dd") }).ToList();
        return new RpCorpPanelRankLogComposer(weeksAgo, CorpWeek.StartedAt(monday), CorpWeek.ResetAt(monday), changes);
    }

    // How long a decided request stays on the Requests page.
    private const int DecidedShownSeconds = 7 * 86400;

    public static RpCorpPanelRequestsComposer BuildRequests(CorpPanel.Viewer viewer)
    {
        List<RequestRow> requests;
        List<Member> members;
        List<Step> ladder;
        Dictionary<int, int> since;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            requests = connection.Query<RequestRow>(RequestSelect +
                "WHERE q.`corporation_id` = @corpId AND (q.`status` = 'pending' OR q.`decided_at` >= @cutoff) ORDER BY q.`created_at` DESC, q.`id` DESC",
                new { corpId = viewer.CorpId, cutoff = (int)UnixTimestamp.GetNow() - DecidedShownSeconds }).ToList();
            members = Members(connection, viewer.CorpId);
            ladder = Ladder(connection, viewer.CorpId);
            since = connection.Query<(int UserId, int At)>(
                    "SELECT `user_id` AS UserId, MAX(`created_at`) AS At FROM `rp_corporation_rank_changes` WHERE `corporation_id` = @corpId GROUP BY `user_id`",
                    new { corpId = viewer.CorpId })
                .ToDictionary(row => row.UserId, row => row.At);
        }
        var counts = CorpDiscipline.CountsForWeek(viewer.CorpId, 0);
        var byUser = members.ToDictionary(member => member.UserId);

        var people = members.Select(member =>
        {
            var up = Next(ladder, member, Promotion);
            var down = Next(ladder, member, Demotion);
            counts.TryGetValue(member.UserId, out var count);
            return new RpCorpPanelRequestsComposer.Person(member.UserId, member.Username, member.Look, member.Gender, Title(member.RankName, member.Tier),
                up?.Title ?? "", down?.Title ?? "",
                Refusal(viewer.UserId, viewer.RankOrder, member, up, Promotion) == null,
                Refusal(viewer.UserId, viewer.RankOrder, member, down, Demotion) == null,
                member.ShiftSecondsWeek + ShiftManager.LiveSessionSeconds(member.UserId), count.Warnings, count.Strikes,
                since.TryGetValue(member.UserId, out var at) ? Math.Max(at, member.HiredAt) : member.HiredAt);
        }).ToList();

        var rows = requests.Select(request =>
        {
            byUser.TryGetValue(request.UserId, out var member);
            var to = member == null ? null : Next(ladder, member, request.Direction);
            var pending = request.Status == "pending";
            // What would stop this viewer approving it right now, worded for the card.
            var blocker = "";
            if (pending)
            {
                if (viewer.CanApprove != 1)
                    blocker = "Your rank can't approve requests.";
                else if (request.RequestedBy == viewer.UserId)
                    blocker = "You asked for this one, so someone else has to decide it.";
                else if (member == null)
                    blocker = "They don't work here any more.";
                else
                    blocker = Refusal(viewer.UserId, viewer.RankOrder, member, to, request.Direction) ?? "";
            }
            return new RpCorpPanelRequestsComposer.Request(request.Id, request.UserId, request.Username, request.Look, request.Gender, request.Direction,
                member == null ? "" : Title(member.RankName, member.Tier), to?.Title ?? "", request.Reason, request.RequestedBy, request.RequestedByName,
                request.Status, request.DecidedByName, request.DecidedAt, request.Note, request.CreatedAt, blocker);
        }).ToList();

        return new RpCorpPanelRequestsComposer(viewer.CanApprove == 1, viewer.ShiftQuota, rows, people);
    }
}
