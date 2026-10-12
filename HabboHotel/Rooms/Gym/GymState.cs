using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
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
/// Stepping off keeps the progress. That progress is shown as XP on the
/// profile (XpOf): 100 to reach level 1, 100 more each level, 1,000 for 10 -
/// the same training time, so XP comes in faster the higher the level.
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
///
/// THE WORKOUT IS SEEN. While training counts - not while paused - the player
/// wears Habbo's own effect for the machine (194 Treadmill, 195 CrossTrainer:
/// the walk played in place), faces along it, and the machine runs (state 1).
/// Paused, off or gone, the effect they wore before comes back and the machine
/// stops once nobody is training on it. Both ends are said in the blue action
/// bubble - "*starts a stamina workout*", "*stops working out*" - except a
/// stop by leaving the room or logging out. So is a level gained: "*feels
/// fitter and levels up their stamina*" / "*feels stronger and levels up
/// their strength*".
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

    /// <summary>Habbo's own workout effects (EffectMap): the walk, played in place.</summary>
    public const int TreadmillEffectId = 194;
    public const int CrossTrainerEffectId = 195;

    private static int EffectFor(Stat stat) => stat == Stat.Stamina ? TreadmillEffectId : CrossTrainerEffectId;

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

    // ---- who is on a machine ----------------------------------------------

    /// <summary>One player standing on a machine: what their panel last showed, and the training not yet banked.</summary>
    private sealed class Session
    {
        public Stat Stat;
        public Item? Item;
        public string Machine = "";
        /// <summary>Wearing the workout effect, with the machine running for them.</summary>
        public bool Animating;
        /// <summary>The effect they wore before it, given back when it comes off.</summary>
        public int PreviousEffect;
        public long LastTick;
        public long Milliseconds;
        public int Unsaved;
        public bool HadMembership;
        public string SentPause = "";
        public int SentLevel = -1;
        public long SentAt;
    }

    private static readonly ConcurrentDictionary<int, Session> Training = new();

    /// <summary>
    /// Seconds after a workout ends before a step onto a machine can start
    /// another - stepping on and off would otherwise fill the room with
    /// bubbles. It is asked on the step only: a workout under way, paused and
    /// resumed, carries on.
    /// </summary>
    private const int CooldownSeconds = 5;

    /// <summary>When each player's last workout ended (Environment.TickCount64).</summary>
    private static readonly ConcurrentDictionary<int, long> LastEnded = new();

    /// <summary>A step onto a machine inside the cooldown, and whether they have been told.</summary>
    private sealed class Refusal
    {
        public uint ItemId;
        public int Wait;
        public bool Told;
    }

    /// <summary>
    /// The machine each player stepped onto inside the cooldown. It holds until
    /// the step OFF that machine (SteppedOff) - not until the cooldown runs out,
    /// and not on a tick that merely finds them mid-step - so they stand still
    /// on it until they step off and back on.
    /// </summary>
    private static readonly ConcurrentDictionary<int, Refusal> Refused = new();

    /// <summary>
    /// Item.UserWalksOnFurni on gym equipment - the game's own step event, which
    /// fires on every arrival on the tile, so no step can fall between ticks.
    /// Inside the cooldown the step is refused (Tick tells them once they stop).
    /// </summary>
    public static void SteppedOn(RoomUser user, Item item)
    {
        var habbo = user?.GetClient()?.GetHabbo();
        if (habbo == null)
            return;
        var wait = CooldownLeft(habbo.Id, Environment.TickCount64);
        if (wait > 0)
            Refused[habbo.Id] = new Refusal { ItemId = item.Id, Wait = wait };
        else
            Refused.TryRemove(habbo.Id, out _);
    }

    /// <summary>
    /// Item.UserWalksOffFurni on gym equipment: the workout on it ends there and
    /// then (the bubble, and the cooldown's start), and a refusal of it is over.
    /// </summary>
    public static void SteppedOff(Room room, RoomUser user, Item item)
    {
        var client = user?.GetClient();
        var habbo = client?.GetHabbo();
        if (habbo == null)
            return;
        if (Refused.TryGetValue(habbo.Id, out var refusal) && refusal.ItemId == item.Id)
            Refused.TryRemove(habbo.Id, out _);
        if (Training.TryGetValue(habbo.Id, out var session) && session.Item?.Id == item.Id)
            Stop(client, habbo, room, user);
    }

    /// <summary>Whole seconds still to wait before a workout can start, 0 when it can.</summary>
    private static int CooldownLeft(int userId, long now)
    {
        if (!LastEnded.TryGetValue(userId, out var ended))
            return 0;
        var leftMs = CooldownSeconds * 1000L - (now - ended);
        return leftMs <= 0 ? 0 : (int)Math.Ceiling(leftMs / 1000.0);
    }

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

        var machine = MachineUnder(room, user);
        if (machine == null)
        {
            // The safety net: a step off ends a workout on the spot
            // (SteppedOff); this catches the rest - the machine picked up or
            // moved, a teleport off it.
            Refused.TryRemove(habbo.Id, out _);
            Stop(client, habbo, room, user);
            return;
        }

        var now = Environment.TickCount64;
        // Walking on it - passing over, or setting off: nothing starts and no
        // training counts, but nothing ends either. Leaving is the step-off
        // event's to decide, so a moment's "walking" without leaving the tile
        // cannot end a workout or undo a refusal.
        if (user.IsWalking)
        {
            if (Training.TryGetValue(habbo.Id, out var walking))
                walking.LastTick = now;
            return;
        }

        var stat = machine.Definition.InteractionType == InteractionType.GymStamina ? Stat.Stamina : Stat.Strength;
        if (!Training.TryGetValue(habbo.Id, out var session) || session.Stat != stat || session.Item?.Id != machine.Id)
        {
            if (session != null)
                Stop(client, habbo, room, user);

            // Stepped on inside the cooldown (SteppedOn): they stand still -
            // told once, in a whisper like :hit's, [5/5] the moment they step
            // on, [1/5] with under a second to go - and nothing starts while
            // they stay on it, even once the cooldown has run out. They step
            // off and back on to start.
            if (Refused.TryGetValue(habbo.Id, out var refusal) && refusal.ItemId == machine.Id)
            {
                if (!refusal.Told)
                {
                    refusal.Told = true;
                    client.SendWhisper($"Cooldown [{refusal.Wait}/{CooldownSeconds}]");
                }
                return;
            }

            session = new Session
            {
                Stat = stat,
                Item = machine,
                Machine = PanelName(machine),
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
        // Seen only while it counts: paused, they just stand there.
        if (pause.Length == 0 && !session.Animating)
            StartAnimating(room, user, habbo, session);
        else if (pause.Length > 0 && session.Animating)
            StopAnimating(habbo, session, room, user);
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

    // ---- the workout, seen ---------------------------------------------------

    /// <summary>The blue action bubble (style 4) every PixelRP action uses: "*Twist starts a stamina workout*".</summary>
    private const int ActionBubble = 4;

    /// <summary>
    /// What the panel calls the machine: the two gym machines by a short name
    /// ("Hi-tech Crosstrainer" is the shop's), anything else given a gym
    /// behaviour by its own.
    /// </summary>
    private static string PanelName(Item machine) => machine.Definition.ItemName switch
    {
        "olympics_c16_treadmill" => "Treadmill",
        "olympics_c16_crosstrainer" => "Crosstrainer",
        _ => machine.Definition.PublicName ?? ""
    };

    /// <summary>Training has started counting: said out loud, the effect on, turned along the machine, the machine running.</summary>
    private static void StartAnimating(Room room, RoomUser user, Habbo habbo, Session session)
    {
        room.SendPacket(new ChatComposer(user.VirtualId,
            $"*starts a {(session.Stat == Stat.Stamina ? "stamina" : "strength")} workout*", 0, ActionBubble));
        var effect = EffectFor(session.Stat);
        if (habbo.Effects != null)
        {
            var current = habbo.Effects.CurrentEffect;
            session.PreviousEffect = current == effect ? 0 : current;
            habbo.Effects.ApplyEffect(effect);
        }
        // The machine's console is at the end it points to (direction 0 up
        // and right, 2 down and right, ...), so facing its rotation faces the
        // console. Set directly, as an arrow does: SetRot only turns the head
        // for a small turn. Once, at the start - turning to look round
        // afterwards is the player's business.
        if (session.Item != null)
        {
            user.RotBody = session.Item.Rotation;
            user.RotHead = session.Item.Rotation;
            user.UpdateNeeded = true;
        }
        session.Animating = true;
        UpdateMachine(session.Item);
    }

    /// <summary>
    /// Paused, off the machine or gone: the effect they wore before comes back
    /// - unless something has replaced the workout effect since (cuffs), which
    /// is left alone - and the machine stops if nobody else is on it. Said out
    /// loud when it stops in front of the room (`room`/`user` given); not when
    /// they leave the room or log out, which would bubble over somebody
    /// already gone.
    /// </summary>
    private static void StopAnimating(Habbo habbo, Session session, Room? room = null, RoomUser? user = null)
    {
        session.Animating = false;
        LastEnded[habbo.Id] = Environment.TickCount64;
        if (room != null && user != null)
            room.SendPacket(new ChatComposer(user.VirtualId, "*stops working out*", 0, ActionBubble));
        if (habbo.Effects != null && habbo.Effects.CurrentEffect == EffectFor(session.Stat))
        {
            var previous = Math.Max(0, session.PreviousEffect);
            habbo.Effects.ApplyEffect(previous);
            // ApplyEffect does nothing for somebody no longer in a room, and an
            // effect follows its wearer into the next one - so it is set here
            // as well, or the workout would walk out of the gym with them.
            habbo.Effects.CurrentEffect = previous;
        }
        UpdateMachine(session.Item);
    }

    /// <summary>The machine runs (state 1) while anybody is training on it - tiles can be shared.</summary>
    private static void UpdateMachine(Item? item)
    {
        if (item == null || item.GetRoom() == null)
            return;
        var want = Training.Values.Any(other => other.Animating && other.Item?.Id == item.Id) ? "1" : "0";
        // Every change is a packet to the whole room.
        if (item.LegacyDataString == want)
            return;
        item.LegacyDataString = want;
        // Not saved: a machine that is running is not a state worth keeping.
        item.UpdateState(false, true);
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
        // Said out loud, like the start and stop of the workout.
        room.SendPacket(new ChatComposer(user.VirtualId, stat == Stat.Stamina
            ? "*feels fitter and levels up their stamina*"
            : "*feels stronger and levels up their strength*", 0, ActionBubble));
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
            level, left, forLevel, MembershipLeft(habbo), pause, session.Item?.Definition.SpriteId ?? 0);
    }

    private static RpGymComposer Hidden(Habbo habbo) =>
        new(false, "", "", 0, 0, 0, MembershipLeft(habbo), "", 0);

    /// <summary>
    /// Off the machine: bank what was trained and take the panel down. `room`
    /// and `user` when they walked off in front of the room, so it is said.
    /// </summary>
    private static void Stop(GameClient? client, Habbo habbo, Room? room = null, RoomUser? user = null)
    {
        if (!Training.TryRemove(habbo.Id, out var session))
            return;
        if (session.Animating)
            StopAnimating(habbo, session, room, user);
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
        if (habbo == null)
            return;
        Refused.TryRemove(habbo.Id, out _);
        Stop(client, habbo);
    }

    /// <summary>Logging out: the online clock stops, and the row is saved as it stands.</summary>
    public static void OnLogout(Habbo habbo)
    {
        // Still in the room at this point, so the machine can be stopped.
        if (Training.TryRemove(habbo.Id, out var session) && session.Animating)
            StopAnimating(habbo, session);
        LastEnded.TryRemove(habbo.Id, out _);
        Refused.TryRemove(habbo.Id, out _);
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

    // ---- XP --------------------------------------------------------------------

    /// <summary>XP to reach `level` from the one below it: 100 for level 1, 100 more each level, 1,000 for 10.</summary>
    public static int XpForLevel(int level) => 100 * Math.Clamp(level, 1, MaxLevel);

    /// <summary>
    /// XP toward the next level, and how much that level takes - (0, 0) at
    /// level 10. It is the training already stored, scaled to the level's XP:
    /// XP has no column of its own and the time a level takes is unchanged, so
    /// it simply comes in faster the higher the level.
    /// </summary>
    public static (int Xp, int Needed) XpOf(int level, int seconds)
    {
        if (level >= MaxLevel)
            return (0, 0);
        var next = level + 1;
        var needed = XpForLevel(next);
        var xp = (int)Math.Min(needed, (long)Math.Max(0, seconds) * needed / SecondsForLevel(next));
        return (xp, needed);
    }

    /// <summary>A player's two levels and the XP toward each next one, for a profile.</summary>
    public readonly record struct Levels(int Strength, int StrengthXp, int StrengthXpNeeded, int Stamina, int StaminaXp, int StaminaXpNeeded);

    /// <summary>A player's gym levels for a profile: live if they are online, else as last saved.</summary>
    public static Levels LevelsOf(int userId)
    {
        int strength, strengthSeconds, stamina, staminaSeconds;
        var online = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.GetHabbo();
        if (online != null)
        {
            online.EnsureRpStatsLoaded();
            (strength, strengthSeconds, stamina, staminaSeconds) =
                (online.RpStrengthLevel, online.RpStrengthSeconds, online.RpStaminaLevel, online.RpStaminaSeconds);
        }
        else
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            var row = connection.QueryFirstOrDefault<LevelsRow>(
                "SELECT `strength_level` AS Strength, `strength_seconds` AS StrengthSeconds, " +
                "`stamina_level` AS Stamina, `stamina_seconds` AS StaminaSeconds FROM `user_rp_stats` WHERE `user_id` = @userId LIMIT 1",
                new { userId });
            (strength, strengthSeconds, stamina, staminaSeconds) = row == null
                ? (0, 0, 0, 0)
                : (row.Strength, row.StrengthSeconds, row.Stamina, row.StaminaSeconds);
        }
        var (strengthXp, strengthNeeded) = XpOf(strength, strengthSeconds);
        var (staminaXp, staminaNeeded) = XpOf(stamina, staminaSeconds);
        return new Levels(strength, strengthXp, strengthNeeded, stamina, staminaXp, staminaNeeded);
    }

    private sealed class LevelsRow
    {
        public int Strength { get; set; }
        public int StrengthSeconds { get; set; }
        public int Stamina { get; set; }
        public int StaminaSeconds { get; set; }
    }
}
