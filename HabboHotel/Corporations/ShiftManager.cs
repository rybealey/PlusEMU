using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.HabboHotel.GameClients;
using Plus.Utilities;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: live shift tracking for :startwork / :stopwork. One in-memory
/// session per on-duty player; a 10-second timer drives minute flushes,
/// countdown whispers and payouts. Progress toward the next pay persists in
/// rp_corporation_employees.pay_seconds, so stopping (or logging out) 3
/// minutes short of payday resumes with 3 minutes left.
/// </summary>
public static class ShiftManager
{
    public const int PayIntervalSeconds = 600;

    private sealed class ShiftSession
    {
        public int UserId;
        public string CorpName = "";
        public int RankPay;
        // pay_seconds already banked in the DB when this session started
        public int BasePaySeconds;
        public double StartedAt;
        // seconds of THIS session already flushed to the DB
        public int FlushedSeconds;
        // payouts already made this session (600s each)
        public int PaidIntervals;
        // last minute boundary we acted on (whisper/flush/pay)
        public int LastMinute;
    }

    private static readonly ConcurrentDictionary<int, ShiftSession> Sessions = new();
    private static System.Threading.Timer _timer;

    public static void Init()
    {
        // stale on-duty flags from a crash; at most ~1 minute of unflushed
        // progress is lost with them
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            connection.Execute("UPDATE `rp_corporation_employees` SET `on_duty` = 0 WHERE `on_duty` = 1");
        _timer = new System.Threading.Timer(_ => Tick(), null, 10000, 10000);
    }

    public static bool IsOnDuty(int userId) => Sessions.ContainsKey(userId);

    // Live seconds of the current session (0 off duty) - composers add this
    // to the persisted counters so viewers see ticking values.
    public static int LiveSessionSeconds(int userId)
        => Sessions.TryGetValue(userId, out var session) ? Elapsed(session) : 0;

    public static void StartShift(GameClient client)
    {
        var userId = client.GetHabbo().Id;
        if (Sessions.ContainsKey(userId))
        {
            client.SendWhisper("You're already on duty.");
            return;
        }
        (int PaySeconds, int Pay, string CorpName)? job;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            job = connection.QuerySingleOrDefault<(int PaySeconds, int Pay, string CorpName)?>(
                "SELECT e.`pay_seconds` AS PaySeconds, r.`pay` AS Pay, c.`name` AS CorpName " +
                "FROM `rp_corporation_employees` e " +
                "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
                "INNER JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
                "WHERE e.`user_id` = @userId LIMIT 1", new { userId });
            if (job == null)
            {
                client.SendWhisper("You don't have a job. Get hired by a corporation first.");
                return;
            }
            connection.Execute("UPDATE `rp_corporation_employees` SET `on_duty` = 1 WHERE `user_id` = @userId LIMIT 1", new { userId });
        }
        var session = new ShiftSession
        {
            UserId = userId,
            CorpName = job.Value.CorpName,
            RankPay = job.Value.Pay,
            BasePaySeconds = job.Value.PaySeconds,
            StartedAt = UnixTimestamp.GetNow()
        };
        Sessions[userId] = session;
        client.SendWhisper($"You are now on duty at {session.CorpName}. {PayMessage(RemainingSeconds(session, 0))}");
    }

    public static void StopShift(GameClient client)
    {
        var userId = client.GetHabbo().Id;
        if (!Sessions.TryRemove(userId, out var session))
        {
            client.SendWhisper("You're not on duty.");
            return;
        }
        var banked = EndSession(session);
        client.SendWhisper($"Off duty. {FormatMinutes(banked)} banked toward your next pay.");
    }

    public static void InterruptForIdle(GameClient client)
    {
        var userId = client?.GetHabbo()?.Id ?? 0;
        if (userId == 0 || !Sessions.TryRemove(userId, out var session)) return;
        var banked = EndSession(session);
        client.SendWhisper($"Your shift ended because you went idle. {FormatMinutes(banked)} banked toward your next pay.");
    }

    public static void InterruptForDisconnect(int userId)
    {
        if (!Sessions.TryRemove(userId, out var session)) return;
        EndSession(session);
    }

    private static int Elapsed(ShiftSession session)
        => (int)(UnixTimestamp.GetNow() - session.StartedAt);

    // total seconds toward the CURRENT pay interval right now
    private static int PayProgress(ShiftSession session, int elapsed)
        => session.BasePaySeconds + elapsed - (session.PaidIntervals * PayIntervalSeconds);

    private static int RemainingSeconds(ShiftSession session, int elapsed)
        => PayIntervalSeconds - PayProgress(session, elapsed);

    private static string PayMessage(int remainingSeconds)
    {
        var minutes = Math.Max(1, (remainingSeconds + 59) / 60);
        return (minutes == 1) ? "Next pay in 1 minute." : $"Next pay in {minutes} minutes.";
    }

    private static string FormatMinutes(int seconds) => $"{seconds / 60}m";

    // Banks the session into the DB (delta counters, absolute pay_seconds),
    // clears on_duty, returns the banked pay_seconds.
    private static int EndSession(ShiftSession session)
    {
        var elapsed = Elapsed(session);
        var paySeconds = PayProgress(session, elapsed);
        Flush(session, elapsed, paySeconds, offDuty: true);
        return paySeconds;
    }

    private static void Flush(ShiftSession session, int elapsed, int paySeconds, bool offDuty)
    {
        var delta = elapsed - session.FlushedSeconds;
        if (delta < 0) delta = 0;
        session.FlushedSeconds = elapsed;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute(
            "UPDATE `rp_corporation_employees` SET " +
            "`pay_seconds` = @paySeconds, " +
            "`shift_seconds` = `shift_seconds` + @delta, " +
            "`shift_seconds_week` = `shift_seconds_week` + @delta" +
            (offDuty ? ", `on_duty` = 0" : "") +
            " WHERE `user_id` = @userId LIMIT 1",
            new { paySeconds, delta, userId = session.UserId });
    }

    private static void Tick()
    {
        foreach (var session in Sessions.Values)
        {
            try
            {
                TickSession(session);
            }
            catch
            {
                // one broken session must not stall the others
            }
        }
    }

    private static void TickSession(ShiftSession session)
    {
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(session.UserId);
        if (client == null)
        {
            // missed the disconnect hook somehow - bank and drop
            InterruptForDisconnect(session.UserId);
            return;
        }
        var elapsed = Elapsed(session);
        var minute = elapsed / 60;
        if (minute <= session.LastMinute) return;
        session.LastMinute = minute;

        var paidThisMinute = false;
        while (PayProgress(session, elapsed) >= PayIntervalSeconds)
        {
            session.PaidIntervals++;
            paidThisMinute = true;
            client.GetHabbo().Credits += session.RankPay;
            client.Send(new CreditBalanceComposer(client.GetHabbo().Credits));
            client.SendWhisper($"Payday! You earned {session.RankPay}c.");
        }

        Flush(session, elapsed, PayProgress(session, elapsed), offDuty: false);

        if (!paidThisMinute)
            client.SendWhisper(PayMessage(RemainingSeconds(session, elapsed)));
    }
}
