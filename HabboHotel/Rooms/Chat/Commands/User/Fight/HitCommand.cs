using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Rooms.Movement;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Fight;

/// <summary>
/// pixelrp fighting system: throw a punch at another player.
///
/// The first command that actually deals damage - :slap emits its bubble and
/// nothing else. Health is Habbo.RpHealth, persisted, with no regen, so every
/// point taken here stays taken until a staff :restore.
///
/// A swing always happens. What decides the outcome is REACH: the attacker's
/// own tile plus the four sharing an edge with it (Manhattan distance &lt;= 1).
/// On one of those the punch lands for 3-5, more with the attacker's gym
/// Strength (GymState.PunchDamage: 18-20 at level 10); anywhere else it is
/// thrown and missed, in public, for nothing. There is no dice roll - in range
/// is always a hit. The shared tile counts because two players standing on one are as
/// close as it gets. :slap reaches identically - one rule for how close a
/// fight has to be - while :push still takes the whole 3x3 with diagonals.
///
/// Either way it is a fight, so it costs the cooldown and it makes the
/// ATTACKER aggressive: 100, which the room tick then drains over 45 seconds
/// (see RoomUserManager.OnCycle). Only the attacker - throwing a punch is what
/// makes you aggressive, being hit is not.
///
/// Safe zones (rooms.is_safe_zone, owner-set under Room settings > Roleplay >
/// Zoning) are where fighting stops - with one exception: two players who are
/// both still aggressive can carry on there until it runs out. Because only
/// swinging earns aggression, that exception needs a MUTUAL fight: both have
/// thrown a punch, so both are flagged, and they can take the brawl through a
/// safe zone until it drains. A one-sided aggressor cannot chase someone who
/// never swung back into one, nobody standing in a safe zone calmly can be
/// dragged into a fight, and an aggressive player cannot walk in and start
/// one.
///
/// Passive players (a smoothie, or City Government on duty) cannot be hit -
/// the untouchable that IsRpPassive has always promised and that nothing
/// could honour until damage existed. A smoothie stops you hitting too; staff
/// on duty may hit (Habbo.CannotFight). Nor can an AFK player (RoomUser.IsAfk)
/// - asleep five minutes - though a stun still lands on them.
/// </summary>
internal class HitCommand : ITargetChatCommand
{
    public string Key => "hit";
    public string PermissionRequired => "command_hit";

    public string Parameters => "%target%";

    public string Description => "Throw a punch at another user.";

    public bool MustBeInSameRoom => true;

    public bool IsRanged => true;

    /// <summary>Combat wording: you pick a target, you do not type a username.</summary>
    public string NoTargetMessage => "No target selected.";

    /// <summary>Blue bubble, the one every combat action shares.</summary>
    private const int FightBubble = 4;

    /// <summary>Seconds between punches. Shorter than the five :slap and the
    /// social commands use - a fight has to be able to trade blows.</summary>
    private const int CooldownSeconds = 3;

    /// <summary>What a swing sets the attacker's aggression to.</summary>
    private const int AggressionOnSwing = 100;

    /// <summary>
    /// Each player's punching cooldown (PlayerCooldown, which forgives a slow
    /// connection). A swing that missed still counts - it was still a punch.
    /// </summary>
    private readonly PlayerCooldown _cooldown = new(CooldownSeconds);

    // A missing username, an offline target and a target in another room are
    // all answered by CommandManager before Execute runs.
    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        if (target == habbo)
        {
            session.SendWhisper("You cannot hit yourself.");
            return Task.CompletedTask;
        }

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser == null)
        {
            session.SendWhisper(RangeMessages.NotInRoom);
            return Task.CompletedTask;
        }

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (thisUser == null)
            return Task.CompletedTask;

        // Passive and health both live in user_rp_stats, so both sides have to
        // be loaded before anything is decided on them.
        habbo.EnsureRpStatsLoaded();
        target.EnsureRpStatsLoaded();

        if (habbo.CannotFight)
        {
            session.SendWhisper("You cannot fight while you are passive.");
            return Task.CompletedTask;
        }

        // Restrained: cuffs stop you fighting, and so does being marched
        // somewhere by whoever put them on you.
        if (Police.PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("You cannot fight while you are cuffed.");
            return Task.CompletedTask;
        }

        if (Police.PoliceState.IsBeingEscorted(habbo.Id))
        {
            session.SendWhisper("You cannot fight while you are being escorted.");
            return Task.CompletedTask;
        }

        if (target.IsRpPassive)
        {
            session.SendWhisper($"{target.Username} is passive and cannot be fought.");
            return Task.CompletedTask;
        }

        if (target.RpHealth <= 0)
        {
            session.SendWhisper(KnockedOut.TargetRefusal);
            return Task.CompletedTask;
        }

        // Away from the keyboard is out of the fight (RoomUser.IsAfk).
        if (targetUser.IsAfk)
        {
            session.SendWhisper("You cannot hit an AFK player.");
            return Task.CompletedTask;
        }

        // The safe-zone rule. Both players are necessarily in the same room,
        // so it is safe or unsafe for the pair of them at once.
        if (room.IsSafeZone && !(habbo.RpAggression > 0 && target.RpAggression > 0))
        {
            session.SendWhisper("You can only fight in an unsafe zone.");
            return Task.CompletedTask;
        }

        if (_cooldown.IsWaiting(habbo.Id, out var secondsLeft))
        {
            session.SendWhisper(_cooldown.Refusal(secondsLeft));
            return Task.CompletedTask;
        }

        // Past here the punch is thrown, so it costs the cooldown and makes the
        // attacker aggressive whether or not it connects.
        _cooldown.Start(habbo.Id);
        habbo.RpAggression = AggressionOnSwing;

        // The attacker's own tile plus the four sharing an edge with it: at most
        // one step, no diagonals. Nought covers two players on a shared tile.
        // Measured between the tiles the two are ON SCREEN, not RoomUser.X/Y,
        // which for anyone mid-step still names the tile they are leaving - a
        // punch thrown walking up to someone fell a tile short. Someone
        // mid-step counts as on the tile they are stepping onto, and on the
        // one they are leaving for the first half of the step; the punch
        // lands from any pairing (MovementV2Bridge.ReachTiles, as :stun).
        var (hitterA, hitterB) = MovementV2Bridge.ReachTiles(thisUser);
        var (targetA, targetB) = MovementV2Bridge.ReachTiles(targetUser);
        static bool Touching(System.Drawing.Point a, System.Drawing.Point b) => (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)) <= 1;
        var inReach = Touching(hitterA, targetA) || Touching(hitterA, targetB)
            || Touching(hitterB, targetA) || Touching(hitterB, targetB);

        // Leading AND trailing "*" matter: the client only treats a style-4
        // bubble as an action when the text is wrapped in them, and it then
        // moves the opening marker ahead of the actor's name, rendering
        // "*Yavn swings at twist, causing 4 damage*".
        if (inReach)
        {
            // Strength is the gym's: the range grows with the attacker's level.
            var (minDamage, maxDamage) = Gym.GymState.PunchDamage(habbo.RpStrengthLevel);
            var dealt = Random.Shared.Next(minDamage, maxDamage + 1);
            // pixelrp durability: worn armour soaks its share and wears by the
            // whole hit; the bubble says what the target's health lost. The
            // weapon in the attacker's hand wears by the whole hit too, below.
            var damage = RpDurability.Absorb(target, dealt);
            target.RpHealth = Math.Max(0, target.RpHealth - damage);
            // pixelrp: a punch ends a bin search, and an officer's trip to a call.
            BinSearch.Interrupt(target.Id);
            Plus.HabboHotel.Corporations.EmergencyCalls.CancelTrip(target.Id);

            // pixelrp consumables: a punch that lands ruins a medkit. The
            // remaining health is not owed back - the kit is spent, and
            // standing still somewhere safe is the price of using one.
            //
            // A LANDED punch only. A swing that misses is not being hit, and
            // neither is a slap, which is why this lives in the inReach branch
            // of :hit and nowhere in :slap.
            //
            // The snack keeps going. Getting hit interrupts bandaging, not
            // eating, which is the line the design drew.
            if (target.RpHealthRegen.Running)
            {
                target.RpHealthRegen.Stop();
                target.Client?.SendWhisper("Your medkit was ruined when you were hit.");
            }

            // Queued (RoomTickWriter), not written here: the reply no longer waits on the database.
            RoomTickWriter.QueueRpStats(target);
            room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*swings at {target.Username}, causing {damage} damage*", 0, FightBubble));
            // the only thing that moved on the target is their health
            SendStats(room, targetUser, target);
            RpDurability.WearWeapon(habbo, dealt);
        }
        else
            room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*swings at {target.Username}, but misses*", 0, FightBubble));

        // The attacker's aggression moved either way.
        SendStats(room, thisUser, habbo);

        // Out of health is out of the fight: the same frozen lay :kill applies.
        if (target.RpHealth <= 0)
            room.GetRoomUserManager().ApplyRpKnockout(targetUser);

        return Task.CompletedTask;
    }

    private static void SendStats(Room room, RoomUser user, Habbo habbo) =>
        room.SendPacket(new RpStatsComposer(user.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax,
            (int)Math.Round(habbo.RpAggression), habbo.IsRpPassive ? 1 : 0, habbo.Rank >= 5 ? 1 : 0));
}
