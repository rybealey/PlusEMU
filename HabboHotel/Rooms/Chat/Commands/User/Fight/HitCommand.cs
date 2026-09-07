using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Fight;

/// <summary>
/// pixelrp fighting system: throw a punch at another player.
///
/// The first command that actually deals damage - :slap emits its bubble and
/// nothing else. Health is Habbo.RpHealth, persisted, with no regen, so every
/// point taken here stays taken until a staff :restore.
///
/// A swing always happens. What decides the outcome is REACH: the four tiles
/// sharing an edge with the attacker's own (Manhattan distance exactly 1). On
/// one of those the punch lands for 3-5; anywhere else it is thrown and
/// missed, in public, for nothing. There is no dice roll - in range is always
/// a hit. Note this is a TIGHTER reach than :slap and :push, which take the
/// whole 3x3 block including the diagonals and the attacker's own tile.
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
/// Passive players (a smoothie, or City Government on duty) neither hit nor
/// get hit - the untouchable that IsRpPassive has always promised and that
/// nothing could honour until damage existed.
/// </summary>
internal class HitCommand : ITargetChatCommand
{
    public string Key => "hit";
    public string PermissionRequired => "command_hit";

    public string Parameters => "%target%";

    public string Description => "Throw a punch at another user.";

    public bool MustBeInSameRoom => true;

    /// <summary>Combat wording: you pick a target, you do not type a username.</summary>
    public string NoTargetMessage => "No target selected.";

    /// <summary>Blue bubble, the one every combat action shares.</summary>
    private const int FightBubble = 4;

    /// <summary>Seconds between punches. Shorter than the five :slap and the
    /// social commands use - a fight has to be able to trade blows.</summary>
    private const int CooldownSeconds = 3;

    /// <summary>Damage a landed punch takes off, inclusive.</summary>
    private const int MinDamage = 3;
    private const int MaxDamage = 5;

    /// <summary>What a swing sets the attacker's aggression to.</summary>
    private const int AggressionOnSwing = 100;

    /// <summary>
    /// Last swing per player id. Commands are DI singletons, so this instance
    /// field is shared hotel-wide; concurrent because rooms tick on their own
    /// threads. A swing that missed still counts - it was still a punch.
    /// </summary>
    private readonly ConcurrentDictionary<int, DateTime> _lastHit = new();

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
            session.SendWhisper($"{target.Username} is not in this room.");
            return Task.CompletedTask;
        }

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (thisUser == null)
            return Task.CompletedTask;

        // Passive and health both live in user_rp_stats, so both sides have to
        // be loaded before anything is decided on them.
        habbo.EnsureRpStatsLoaded();
        target.EnsureRpStatsLoaded();

        if (habbo.IsRpPassive)
        {
            session.SendWhisper("You cannot fight while you are passive.");
            return Task.CompletedTask;
        }

        if (target.IsRpPassive)
        {
            session.SendWhisper($"{target.Username} is passive and cannot be fought.");
            return Task.CompletedTask;
        }

        if (target.RpHealth <= 0)
        {
            session.SendWhisper($"{target.Username} is already out cold.");
            return Task.CompletedTask;
        }

        // The safe-zone rule. Both players are necessarily in the same room,
        // so it is safe or unsafe for the pair of them at once.
        if (room.IsSafeZone && !(habbo.RpAggression > 0 && target.RpAggression > 0))
        {
            session.SendWhisper("You can only fight in an unsafe zone.");
            return Task.CompletedTask;
        }

        if (_lastHit.TryGetValue(habbo.Id, out var last))
        {
            var elapsed = (DateTime.UtcNow - last).TotalSeconds;
            if (elapsed < CooldownSeconds)
            {
                // Counts DOWN the seconds still to wait, the same way :slap
                // does: [3/3] the instant you retry, [1/3] with under a second
                // to go.
                var remaining = (int)Math.Ceiling(CooldownSeconds - elapsed);
                session.SendWhisper($"Cooldown [{remaining}/{CooldownSeconds}]");
                return Task.CompletedTask;
            }
        }

        // Past here the punch is thrown, so it costs the cooldown and makes the
        // attacker aggressive whether or not it connects.
        _lastHit[habbo.Id] = DateTime.UtcNow;
        habbo.RpAggression = AggressionOnSwing;

        // The four tiles sharing an edge with the attacker's: exactly one step,
        // no diagonals, and not the attacker's own tile.
        var inReach = (Math.Abs(targetUser.X - thisUser.X) + Math.Abs(targetUser.Y - thisUser.Y)) == 1;

        // Leading AND trailing "*" matter: the client only treats a style-4
        // bubble as an action when the text is wrapped in them, and it then
        // moves the opening marker ahead of the actor's name, rendering
        // "*Yavn swings at twist, causing 4 damage*".
        if (inReach)
        {
            var damage = Random.Shared.Next(MinDamage, MaxDamage + 1);
            target.RpHealth = Math.Max(0, target.RpHealth - damage);
            target.SaveRpStats();
            room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*swings at {target.Username}, causing {damage} damage*", 0, FightBubble));
            // the only thing that moved on the target is their health
            SendStats(room, targetUser, target);
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
