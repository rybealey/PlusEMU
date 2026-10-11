using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Banking;

namespace Plus.HabboHotel.Rooms.Gym;

/// <summary>
/// pixelrp: the gym - Strength and Stamina, levels 0 to 10 each.
///
/// STAMINA IS HEALTH. Max health is 100 + 10 per Stamina level, so 100 at
/// level 0 and 200 at level 10; a level gained adds 10 to current health too,
/// so a full bar stays full. STRENGTH IS :hit damage only (PunchDamage):
/// 3-5 at level 0, 1.5 more per level, 18-20 at level 10.
///
/// TRAINING is standing still on gym equipment - the treadmill trains
/// Stamina, the crosstrainer Strength (behaviours GymStamina / GymStrength,
/// migration 245) - with membership time left. It runs on the room tick and
/// pauses while the player is asleep, on duty, knocked out, cuffed, escorted
/// or in jail; passive is fine. Each stat takes five hours: 48 minutes for
/// level 1 and four fewer for each level after, down to 12 for level 10.
/// Stepping off keeps the progress.
///
/// MEMBERSHIP is bought from a Gym bot (GymBot): 30c for an hour of ONLINE
/// time, stackable. It counts down whenever the player is logged in, anywhere.
///
/// THE STAMINA DROP. After 30 hours online without a treadmill workout,
/// Stamina loses one level - once - and the progress toward the next level is
/// wiped, so the lost level is trained again from the start. Never below 100
/// health. Any workout on the treadmill restarts the clock and re-arms the
/// drop, including at level 10, where there is nothing left to gain.
///
/// Both clocks are readings of Habbo.RpOnlineSeconds, the gym's own record of
/// time logged in (see there for why the hotel's onlineTime cannot be used).
/// </summary>
public static class GymState
{
    public const int MaxLevel = 10;
    public const int BaseHealth = 100;
    public const int HealthPerLevel = 10;

    public const int MembershipCost = 30;
    public const int MembershipSeconds = 60 * 60;

    public const int StaminaDropAfterSeconds = 30 * 60 * 60;

    /// <summary>Training seconds between saves, so a crash loses at most a minute.</summary>
    private const int SaveEverySeconds = 60;

    /// <summary>The panel counts down by itself; this re-syncs it now and then.</summary>
    private const long ResendEveryMs = 30_000;

    public enum Stat
    {
        Strength,
        Stamina
    }

    public static int MaxHealthFor(int staminaLevel) =>
        BaseHealth + HealthPerLevel * Math.Clamp(staminaLevel, 0, MaxLevel);

    /// <summary>Seconds of training from level - 1 to `level`: 48 minutes for 1 down to 12 for 10, 300 in all.</summary>
    public static int SecondsForLevel(int level) => (52 - 4 * Math.Clamp(level, 1, MaxLevel)) * 60;

    /// <summary>A landed punch's damage range, inclusive: 3-5, then 1.5 more per level (rounded down), 18-20 at 10.</summary>
    public static (int Min, int Max) PunchDamage(int strengthLevel)
    {
        var bonus = 3 * Math.Clamp(strengthLevel, 0, MaxLevel) / 2;
        return (3 + bonus, 5 + bonus);
    }

    public static int MembershipLeft(Habbo habbo) => Math.Max(0, habbo.RpGymUntil - habbo.RpOnlineSeconds);

    public static int LevelOf(Habbo habbo, Stat stat) => stat == Stat.Stamina ? habbo.RpStaminaLevel : habbo.RpStrengthLevel;

    private static int ProgressOf(Habbo habbo, Stat stat) => stat == Stat.Stamina ? habbo.RpStaminaSeconds : habbo.RpStrengthSeconds;

    private static string NameOf(Stat stat) => stat == Stat.Stamina ? "Stamina" : "Strength";

    // ---- who is on a machine ----------------------------------------------

    /// <summary>One player standing on a machine: what their panel last showed, and the training not yet banked.</summary>
    private sealed class Session
    {
        public Stat Stat;
        public string Machine = "";
        public long LastTick;
        public long Milliseconds;
        public int Unsaved;
        public bool HadMembership;
        public string SentPause = "";
        public int SentLevel = -1;
        public long SentAt;
    }

    private static readonly ConcurrentDictionary<int, Session> Training = new();

    /// <summary>The gym equipment under this unit, or null.</summary>
    public static Item? MachineUnder(Room room, RoomUser user)
    {
        var items = room?.GetGameMap()?.GetAllRoomItemForSquare(user.X, user.Y);
        if (items == null)
            return null;
        foreach (var item in items)
        {
            var type = item?.Definition?.InteractionType;
            if (type == InteractionType.GymStamina || type == InteractionType.GymStrength)
                return item;
        }
        return null;
    }

    /// <summary>
    /// Every room tick, for every player in the room (RoomUserManager.OnCycle).
    /// Cheap when they are nowhere near a machine: two comparisons for the
    /// Stamina drop and one tile lookup.
    /// </summary>
    public static void Tick(Room room, RoomUser user)
    {
        var client = user.GetClient();
        if (client == null)
            return;
        var habbo = client.GetHabbo();
        if (habbo == null || !habbo.RpStatsLoaded)
            return;

        CheckStaminaDrop(room, user, habbo);

        // Standing ON it: somebody walking across a treadmill is not on it.
        var machine = user.IsWalking ? null : MachineUnder(room, user);
        if (machine == null)
        {
            Stop(client, habbo);
            return;
        }

        var stat = machine.Definition.InteractionType == InteractionType.GymStamina ? Stat.Stamina : Stat.Strength;
        var now = Environment.TickCount64;
        if (!Training.TryGetValue(habbo.Id, out var session) || session.Stat != stat)
        {
            if (session != null && session.Unsaved > 0)
                RoomTickWriter.QueueRpStats(habbo);
            session = new Session
            {
                Stat = stat,
                Machine = machine.Definition.PublicName ?? "",
                LastTick = now,
                HadMembership = MembershipLeft(habbo) > 0
            };
            Training[habbo.Id] = session;
        }

        var hasMembership = MembershipLeft(habbo) > 0;
        if (session.HadMembership && !hasMembership)
            client.SendWhisper("Your gym membership has run out.");
        session.HadMembership = hasMembership;

        var pause = PauseReason(user, habbo, stat, hasMembership);
        // Capped, so a stalled tick cannot hand out a burst of training.
        var elapsed = Math.Clamp(now - session.LastTick, 0, 2000);
        session.LastTick = now;
        if (pause.Length == 0)
        {
            // Any workout on the treadmill restarts the Stamina clock and
            // re-arms the drop - at level 10 too, where nothing else happens.
            if (stat == Stat.Stamina)
            {
                habbo.RpStaminaTrainedAt = habbo.RpOnlineSeconds;
                habbo.RpStaminaDropped = false;
            }
            session.Milliseconds += elapsed;
            var seconds = (int)(session.Milliseconds / 1000);
            if (seconds > 0)
            {
                session.Milliseconds -= seconds * 1000L;
                Train(room, user, habbo, stat, seconds);
                session.Unsaved += seconds;
                if (session.Unsaved >= SaveEverySeconds)
                {
                    session.Unsaved = 0;
                    // Queued, never written here: this runs under the room's
                    // _cycleLock (RoomTickWriter).
                    RoomTickWriter.QueueRpStats(habbo);
                }
            }
        }

        var level = LevelOf(habbo, stat);
        if (pause != session.SentPause || level != session.SentLevel || now - session.SentAt >= ResendEveryMs)
        {
            session.SentPause = pause;
            session.SentLevel = level;
            session.SentAt = now;
            client.Send(Panel(habbo, session, pause));
        }
    }

    /// <summary>Why training is not counting right now, as the panel says it - or "" when it is.</summary>
    private static string PauseReason(RoomUser user, Habbo habbo, Stat stat, bool hasMembership)
    {
        // Strength at 10 has nothing left to train, so nothing to pause.
        if (stat == Stat.Strength && habbo.RpStrengthLevel >= MaxLevel)
            return "";
        if (!hasMembership)
            return "Buy a membership from the Gym bot";
        if (ShiftManager.IsOnDuty(habbo.Id))
            return "Paused: you're on duty";
        if (user.RpKnockedOut)
            return "Paused: you're knocked out";
        if (PoliceState.IsCuffed(habbo.Id))
            return "Paused: you're cuffed";
        if (PoliceState.IsBeingEscorted(habbo.Id))
            return "Paused: you're being escorted";
        if (JailState.IsJailed(habbo.Id))
            return "Paused: you're in jail";
        if (user.IsAsleep)
            return "Paused: you're asleep";
        return "";
    }

    private static void Train(Room room, RoomUser user, Habbo habbo, Stat stat, int seconds)
    {
        var level = LevelOf(habbo, stat);
        if (level >= MaxLevel)
            return;
        var progress = ProgressOf(habbo, stat) + seconds;
        var needed = SecondsForLevel(level + 1);
        if (progress < needed)
        {
            SetProgress(habbo, stat, progress);
            return;
        }

        level++;
        SetLevel(habbo, stat, level);
        SetProgress(habbo, stat, level >= MaxLevel ? 0 : progress - needed);
        if (stat == Stat.Stamina)
        {
            habbo.RpHealthMax = MaxHealthFor(level);
            habbo.RpHealth = Math.Min(habbo.RpHealthMax, habbo.RpHealth + HealthPerLevel);
            SendStats(room, user, habbo);
        }
        user.GetClient()?.SendWhisper($"Your {NameOf(stat)} is now level {level}.");
        RoomTickWriter.QueueRpStats(habbo);
    }

    private static void SetLevel(Habbo habbo, Stat stat, int level)
    {
        if (stat == Stat.Stamina)
            habbo.RpStaminaLevel = level;
        else
            habbo.RpStrengthLevel = level;
    }

    private static void SetProgress(Habbo habbo, Stat stat, int seconds)
    {
        if (stat == Stat.Stamina)
            habbo.RpStaminaSeconds = seconds;
        else
            habbo.RpStrengthSeconds = seconds;
    }

    /// <summary>
    /// 30 hours online since the last treadmill workout: one Stamina level
    /// gone, and the progress toward the next one with it. Once - the flag
    /// holds it until the next workout.
    /// </summary>
    private static void CheckStaminaDrop(Room room, RoomUser user, Habbo habbo)
    {
        if (habbo.RpStaminaLevel <= 0 || habbo.RpStaminaDropped)
            return;
        if (habbo.RpOnlineSeconds - habbo.RpStaminaTrainedAt < StaminaDropAfterSeconds)
            return;

        habbo.RpStaminaLevel--;
        habbo.RpStaminaSeconds = 0;
        habbo.RpStaminaDropped = true;
        habbo.RpHealthMax = MaxHealthFor(habbo.RpStaminaLevel);
        habbo.RpHealth = Math.Min(habbo.RpHealth, habbo.RpHealthMax);
        SendStats(room, user, habbo);
        user.GetClient()?.SendWhisper($"You haven't trained in a while, so your Stamina dropped to level {habbo.RpStaminaLevel}.");
        RoomTickWriter.QueueRpStats(habbo);
    }

    private static void SendStats(Room room, RoomUser user, Habbo habbo) =>
        room.SendPacket(new RpStatsComposer(user.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax,
            (int)Math.Round(habbo.RpAggression), habbo.IsRpPassive ? 1 : 0, habbo.Rank >= 5 ? 1 : 0));

    // ---- the panel -------------------------------------------------------

    private static RpGymComposer Panel(Habbo habbo, Session session, string pause)
    {
        var level = LevelOf(habbo, session.Stat);
        var forLevel = level >= MaxLevel ? 0 : SecondsForLevel(level + 1);
        var left = forLevel == 0 ? 0 : Math.Max(0, forLevel - ProgressOf(habbo, session.Stat));
        return new RpGymComposer(true, session.Stat == Stat.Stamina ? "stamina" : "strength", session.Machine,
            level, left, forLevel, MembershipLeft(habbo), pause);
    }

    private static RpGymComposer Hidden(Habbo habbo) =>
        new(false, "", "", 0, 0, 0, MembershipLeft(habbo), "");

    /// <summary>Off the machine: bank what was trained and take the panel down.</summary>
    private static void Stop(GameClient? client, Habbo habbo)
    {
        if (!Training.TryRemove(habbo.Id, out var session))
            return;
        if (session.Unsaved > 0)
            RoomTickWriter.QueueRpStats(habbo);
        client?.Send(Hidden(habbo));
    }

    /// <summary>
    /// The membership as it stands, for the Gym bot's menu - on room entry,
    /// and after anything changes it or the levels: on a machine, the panel
    /// is re-sent on the next tick instead.
    /// </summary>
    public static void Refresh(GameClient? client)
    {
        var habbo = client?.GetHabbo();
        if (habbo == null)
            return;
        habbo.EnsureRpStatsLoaded();
        if (Training.TryGetValue(habbo.Id, out var session))
            session.SentAt = 0;
        else
            client!.Send(Hidden(habbo));
    }

    /// <summary>Leaving the room: off the machine, whatever it was.</summary>
    public static void Forget(GameClient? client)
    {
        var habbo = client?.GetHabbo();
        if (habbo != null)
            Stop(client, habbo);
    }

    /// <summary>Logging out: the online clock stops, and the row is saved as it stands.</summary>
    public static void OnLogout(Habbo habbo)
    {
        Training.TryRemove(habbo.Id, out _);
        if (!habbo.RpStatsLoaded)
            return;
        habbo.StopRpOnlineClock();
        RoomTickWriter.QueueRpStats(habbo);
    }

    // ---- buying, and staff -------------------------------------------------

    /// <summary>One hour of membership for 30c from the purse, or false when they cannot pay.</summary>
    public static bool BuyMembership(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return false;
        habbo.EnsureRpStatsLoaded();
        if (habbo.Credits < MembershipCost)
            return false;

        habbo.Credits -= MembershipCost;
        CoinLedger.Record(habbo, -MembershipCost, "Gym membership");
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            connection.Execute("UPDATE `users` SET `credits` = `credits` - @amount WHERE `id` = @userId LIMIT 1",
                new { amount = MembershipCost, userId = habbo.Id });
        session!.Send(new CreditBalanceComposer(habbo.Credits));

        // Stacked: an hour on top of whatever is left.
        habbo.RpGymUntil = Math.Max(habbo.RpGymUntil, habbo.RpOnlineSeconds) + MembershipSeconds;
        habbo.SaveRpStats();
        Refresh(session);
        return true;
    }

    /// <summary>
    /// :setgym - a level set outright, its progress cleared. A Stamina level
    /// also sets max health and restarts the drop's clock.
    /// </summary>
    public static void SetLevelForStaff(Habbo habbo, Stat stat, int level)
    {
        habbo.EnsureRpStatsLoaded();
        level = Math.Clamp(level, 0, MaxLevel);
        SetLevel(habbo, stat, level);
        SetProgress(habbo, stat, 0);
        if (stat == Stat.Stamina)
        {
            habbo.RpHealthMax = MaxHealthFor(level);
            habbo.RpHealth = Math.Min(habbo.RpHealth, habbo.RpHealthMax);
            habbo.RpStaminaTrainedAt = habbo.RpOnlineSeconds;
            habbo.RpStaminaDropped = false;
        }
        habbo.SaveRpStats();
        Refresh(habbo.Client);
    }

    /// <summary>A player's two levels for a profile: live if they are online, else as last saved.</summary>
    public static (int Strength, int Stamina) LevelsOf(int userId)
    {
        var online = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.GetHabbo();
        if (online != null)
        {
            online.EnsureRpStatsLoaded();
            return (online.RpStrengthLevel, online.RpStaminaLevel);
        }
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var row = connection.QueryFirstOrDefault<LevelsRow>(
            "SELECT `strength_level` AS Strength, `stamina_level` AS Stamina FROM `user_rp_stats` WHERE `user_id` = @userId LIMIT 1",
            new { userId });
        return row == null ? (0, 0) : (row.Strength, row.Stamina);
    }

    private sealed class LevelsRow
    {
        public int Strength { get; set; }
        public int Stamina { get; set; }
    }
}
