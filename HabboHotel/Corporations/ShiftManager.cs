using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
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
        // consecutive minute boundaries spent with no CurrentRoom (hotel
        // view); resets to 0 the moment the player is back in a room
        public int NoRoomMinutes;
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
        var banked = EndSession(session, client);
        client.SendWhisper($"Off duty. {FormatMinutes(banked)} banked toward your next pay.");
    }

    public static void InterruptForIdle(GameClient client)
    {
        var userId = client?.GetHabbo()?.Id ?? 0;
        if (userId == 0 || !Sessions.TryRemove(userId, out var session)) return;
        var banked = EndSession(session, client);
        client.SendWhisper($"Your shift ended because you went idle. {FormatMinutes(banked)} banked toward your next pay.");
    }

    // Disconnect path when the caller already holds the Habbo (Habbo.OnDisconnect).
    // The connection is gone by this point, so payout is silent: no composer, no
    // whisper - just the raw credit mutation, which the disconnect save that runs
    // right after this hook persists along with everything else.
    public static void InterruptForDisconnect(Habbo habbo)
    {
        if (habbo == null || !Sessions.TryRemove(habbo.Id, out var session)) return;
        lock (session)
        {
            var elapsed = Elapsed(session);
            while (PayProgress(session, elapsed) >= PayIntervalSeconds)
            {
                session.PaidIntervals++;
                habbo.Credits += session.RankPay;
                PersistCredits(session.UserId, session.RankPay);
            }
            var paySeconds = PayProgress(session, elapsed);
            Flush(session, elapsed, paySeconds, offDuty: true);
        }
    }

    // Disconnect path when only the user id is known (TickSession's null-client
    // fallback, SuperFireCommand). Resolves the client - if it's actually still
    // around (e.g. the fired player is online) drain and notify normally through
    // it; if nothing resolves there's no one to credit safely, so leave the
    // overflow banked as pay_seconds (pre-existing behavior) for it to be paid
    // out next tick or next shift.
    public static void InterruptForDisconnect(int userId)
    {
        if (!Sessions.TryRemove(userId, out var session)) return;
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        if (client != null)
        {
            EndSession(session, client);
            return;
        }
        lock (session)
        {
            var elapsed = Elapsed(session);
            Flush(session, elapsed, PayProgress(session, elapsed), offDuty: true);
        }
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

    // Drains any pay interval(s) the session completed before it ended, then
    // banks the remainder into the DB (delta counters, absolute pay_seconds),
    // clears on_duty, and returns the banked (sub-600) remainder. Used by
    // StopShift/InterruptForIdle and the InterruptForDisconnect(int) path
    // that resolved a live client - all three still have a connection to
    // pay through.
    private static int EndSession(ShiftSession session, GameClient client)
    {
        lock (session)
        {
            var elapsed = Elapsed(session);
            DrainCompletedIntervals(session, client, elapsed);
            var paySeconds = PayProgress(session, elapsed);
            Flush(session, elapsed, paySeconds, offDuty: true);
            return paySeconds;
        }
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
        lock (session)
        {
            var elapsed = Elapsed(session);
            var minute = elapsed / 60;
            if (minute <= session.LastMinute) return;
            session.LastMinute = minute;

            if (client.GetHabbo().CurrentRoom == null)
            {
                session.NoRoomMinutes++;
                // two consecutive room-less minute boundaries - clock out
                // like InterruptForIdle. There's no room to whisper into,
                // but SendWhisper is already a null-room no-op, so reusing
                // the idle end-session path is safe and avoids duplicating
                // the drain/flush logic.
                if (session.NoRoomMinutes >= 2)
                {
                    Sessions.TryRemove(session.UserId, out _);
                    var banked = EndSession(session, client);
                    client.SendWhisper($"Your shift ended because you went idle. {FormatMinutes(banked)} banked toward your next pay.");
                    return;
                }
            }
            else
            {
                session.NoRoomMinutes = 0;
            }

            var paidIntervalsBefore = session.PaidIntervals;
            DrainCompletedIntervals(session, client, elapsed);
            var paidThisMinute = session.PaidIntervals > paidIntervalsBefore;

            Flush(session, elapsed, PayProgress(session, elapsed), offDuty: false);

            if (!paidThisMinute)
                client.SendWhisper(PayMessage(RemainingSeconds(session, elapsed)));
        }
    }

    // Pays out every pay interval the session has completed as of `elapsed`,
    // crediting the player and notifying them for each one. Shared by
    // TickSession (still connected, polling every 10s) and EndSession
    // (stopwork / idle) so a shift ending mid-interval still pays out
    // whatever it completed instead of banking pay_seconds above the
    // 600 cap.
    private static void DrainCompletedIntervals(ShiftSession session, GameClient client, int elapsed)
    {
        while (PayProgress(session, elapsed) >= PayIntervalSeconds)
        {
            session.PaidIntervals++;
            client.GetHabbo().Credits += session.RankPay;
            PersistCredits(session.UserId, session.RankPay);
            client.Send(new CreditBalanceComposer(client.GetHabbo().Credits));
            client.SendWhisper($"Payday! You earned {session.RankPay}c.");
        }
    }

    // Crash hedge: writes the payout straight to the DB row alongside the
    // in-memory credit bump, so a crash before the next absolute save (the
    // disconnect save, or a later periodic one) doesn't lose a wage. The
    // later save still writes the same in-memory total on top of this, so
    // the two stay consistent.
    private static void PersistCredits(int userId, int amount)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        connection.Execute("UPDATE `users` SET `credits` = `credits` + @amount WHERE `id` = @userId LIMIT 1", new { amount, userId });
    }
}
