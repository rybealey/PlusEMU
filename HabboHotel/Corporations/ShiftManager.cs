using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
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

    private static readonly string[] TierNumerals = { "I", "II", "III", "IV", "V" };

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
        // in-memory only: shown on the infostand while on duty; the DB motto
        // is never touched, so disconnect/crash revert for free
        public string WorkingMotto = "";
        // pixelrp: corp/rank captured at clock-in (kept for context; the
        // minute-tick permission re-check reads the live room instead).
        public int CorpId;
        public int RankId;
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
        var permit = CorporationUtility.EvaluateWork(client.GetHabbo(), isStart: true);
        if (!permit.Ok)
        {
            client.SendWhisper(permit.Reason);
            return;
        }
        (int PaySeconds, int Pay, string CorpName, string Acronym, string RankName, int Tier, int Tiers, int CorpId, int RankId)? job;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            job = connection.QuerySingleOrDefault<(int PaySeconds, int Pay, string CorpName, string Acronym, string RankName, int Tier, int Tiers, int CorpId, int RankId)?>(
                "SELECT e.`pay_seconds` AS PaySeconds, r.`pay` AS Pay, c.`name` AS CorpName, c.`acronym` AS Acronym, " +
                "r.`name` AS RankName, e.`tier` AS Tier, r.`tiers` AS Tiers, e.`corporation_id` AS CorpId, e.`rank_id` AS RankId " +
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
            StartedAt = UnixTimestamp.GetNow(),
            CorpId = job.Value.CorpId,
            RankId = job.Value.RankId
        };
        session.WorkingMotto = BuildWorkingMotto(session.CorpName, job.Value.Acronym, job.Value.RankName, job.Value.Tier, job.Value.Tiers);
        Sessions[userId] = session;
        client.SendWhisper($"You are now on duty at {session.CorpName}. {PayMessage(RemainingSeconds(session, 0))}");
        ApplyMotto(client, session.WorkingMotto);
        AnnounceShift(client, $"*has started their shift at {session.CorpName}*");
    }

    // Acronym on line one, rank on line two; the client's motto elements
    // render the newline via white-space: pre-line. '' acronym falls back
    // to the full name so the motto never renders blank.
    private static string BuildWorkingMotto(string corpName, string acronym, string rankName, int tier, int tiers)
    {
        var tierSuffix = ((tiers > 0 && tier >= 1)
            ? " " + TierNumerals[Math.Min(tier, TierNumerals.Length) - 1]
            : "");
        var corpLabel = (string.IsNullOrEmpty(acronym) ? corpName : acronym);
        return $"[WORKING] {corpLabel}\n{rankName}{tierSuffix}";
    }

    // Room shout in the hired-target bubble (4) - clocking in/out is public.
    private static void AnnounceShift(GameClient client, string message)
    {
        var habbo = client?.GetHabbo();
        var roomUser = habbo?.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        roomUser?.OnChat(4, message, true);
    }

    // A rank/tier/corp change while on duty: refresh the session's wage and
    // working motto so the change shows immediately and the next payday pays
    // the new rank. No-op off duty or when the employment row is gone.
    public static void RefreshSession(int userId)
    {
        if (!Sessions.TryGetValue(userId, out var session)) return;
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        if (client == null) return;
        (string CorpName, string Acronym, string RankName, int Tier, int Tiers, int Pay)? job;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            job = connection.QuerySingleOrDefault<(string CorpName, string Acronym, string RankName, int Tier, int Tiers, int Pay)?>(
                "SELECT c.`name` AS CorpName, c.`acronym` AS Acronym, r.`name` AS RankName, e.`tier` AS Tier, r.`tiers` AS Tiers, r.`pay` AS Pay " +
                "FROM `rp_corporation_employees` e " +
                "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
                "INNER JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
                "WHERE e.`user_id` = @userId LIMIT 1", new { userId });
        if (job == null) return;
        lock (session)
        {
            session.CorpName = job.Value.CorpName;
            session.RankPay = job.Value.Pay;
            session.WorkingMotto = BuildWorkingMotto(job.Value.CorpName, job.Value.Acronym, job.Value.RankName, job.Value.Tier, job.Value.Tiers);
        }
        ApplyMotto(client, session.WorkingMotto);
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
        RevertMotto(client);
        AnnounceShift(client, $"*has ended their shift at {session.CorpName}*");
    }

    public static void InterruptForIdle(GameClient client)
    {
        var userId = client?.GetHabbo()?.Id ?? 0;
        if (userId == 0 || !Sessions.TryRemove(userId, out var session)) return;
        EndSession(session, client);
        RevertMotto(client);
        AnnounceShift(client, "*has fallen asleep on duty*");
    }

    // pixelrp: clocked out because they're no longer in a room they may
    // work in (walked out, rank deauthorized mid-shift, or the room was
    // (re)assigned as a headquarters). Shown exactly like a normal :stopwork
    // - the same blue "has ended their shift" action shout - so it reads as
    // a clean end of shift rather than a special interruption.
    private static void InterruptForLeftWork(GameClient client, ShiftSession session)
    {
        AnnounceShift(client, $"*has ended their shift at {session.CorpName}*");
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
            RevertMotto(client);
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
        return (minutes == 1)
            ? "You'll receive your next paycheck in 1 minute."
            : $"You'll receive your next paycheck in {minutes} minutes.";
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

    // Sets the in-memory motto and pushes it to the infostand (self + room).
    // users.motto is deliberately never written: the DB always holds the real
    // RP-managed motto, so any reload (relog, crash) self-heals.
    private static void ApplyMotto(GameClient client, string motto)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null) return;
        habbo.Motto = motto;
        var room = habbo.CurrentRoom;
        var roomUser = room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (roomUser == null) return;
        client.Send(new UserChangeComposer(roomUser, true));
        room.SendPacket(new UserChangeComposer(roomUser, false));
    }

    private static void RevertMotto(GameClient client)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null) return;
        string motto;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            motto = connection.QuerySingleOrDefault<string>(
                "SELECT `motto` FROM `users` WHERE `id` = @userId LIMIT 1", new { userId = habbo.Id }) ?? "";
        ApplyMotto(client, motto);
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
                // Two consecutive room-less minute boundaries - clock out
                // like InterruptForIdle. No message: there's no room for the
                // asleep-on-duty shout (or a whisper) to render into.
                if (session.NoRoomMinutes >= 2)
                {
                    Sessions.TryRemove(session.UserId, out _);
                    EndSession(session, client);
                    RevertMotto(client);
                    return;
                }
            }
            else
            {
                session.NoRoomMinutes = 0;
                // Re-check permission every minute they're in a room: leaving
                // an authorized workplace, a rank deauthorized mid-shift, an
                // emergency service switched off, or the room being (re)assigned
                // as a headquarters all clock the worker out. Room-less minutes
                // fall through to the 2-minute grace above instead.
                var permit = CorporationUtility.EvaluateWork(client.GetHabbo(), isStart: false);
                if (!permit.Ok)
                {
                    Sessions.TryRemove(session.UserId, out _);
                    EndSession(session, client);
                    RevertMotto(client);
                    InterruptForLeftWork(client, session);
                    return;
                }
            }

            var paidIntervalsBefore = session.PaidIntervals;
            DrainCompletedIntervals(session, client, elapsed);
            var paidThisMinute = session.PaidIntervals > paidIntervalsBefore;

            Flush(session, elapsed, PayProgress(session, elapsed), offDuty: false);

            // countdown every OTHER minute - once a minute reads as spam
            if (!paidThisMinute && (minute % 2 == 0))
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
