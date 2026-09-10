using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Fight;

/// <summary>
/// pixelrp fighting system: slap another player.
///
/// The light end of combat: 1 damage against :hit's 3-5, over the same reach
/// - the slapper's own tile plus the four sharing an edge with it (Manhattan
/// distance &lt;= 1, no diagonals). The two attacks deliberately reach alike so
/// there is one rule for how close a fight has to be; what separates them is
/// what they cost the target. Note :push still uses the wider 3x3.
///
/// It only bites in an unsafe zone. In a safe one a slap is pure flavour: the
/// bubble goes out, nobody loses health and nobody becomes aggressive. That
/// last part matters - if a harmless slap still made you aggressive, two
/// players could slap each other inside a safe zone to flag them both and so
/// unlock :hit there, which is exactly what the zone is meant to prevent.
///
/// Where it does bite it behaves like the rest of combat: the slapper becomes
/// aggressive (100, drained by the room tick over 45 seconds), passive
/// players neither slap nor get slapped, and a target taken to 0 health drops
/// into the same frozen lay :kill applies.
/// </summary>
internal class SlapCommand : ITargetChatCommand
{
    public string Key => "slap";
    public string PermissionRequired => "command_slap";

    public string Parameters => "%target%";

    public string Description => "Slap another user across the face.";

    public bool MustBeInSameRoom => true;

    /// <summary>Combat wording: you pick a target, you do not type a username.</summary>
    public string NoTargetMessage => "No target selected.";

    /// <summary>
    /// Blue bubble. Combat actions share one style so they read as a single
    /// system at a glance, distinct from ordinary chat and from the white
    /// star bubble the staff RP commands use.
    /// </summary>
    private const int FightBubble = 4;

    /// <summary>Seconds a player must wait between slaps. Longer than :hit's
    /// three, to go with the lighter damage.</summary>
    private const int CooldownSeconds = 5;

    /// <summary>Health a slap takes off, in an unsafe zone.</summary>
    private const int Damage = 1;

    /// <summary>What a slap that lands sets the slapper's aggression to.</summary>
    private const int AggressionOnSlap = 100;

    /// <summary>
    /// Last successful slap per player id. Commands are DI singletons, so this
    /// instance field is shared hotel-wide; concurrent because rooms tick on
    /// their own threads. Only successful slaps are recorded, so one that missed
    /// for range costs nothing.
    /// </summary>
    private readonly ConcurrentDictionary<int, DateTime> _lastSlap = new();

    // Missing username, an offline target and a target in another room are all
    // answered by CommandManager before Execute runs, so they are not repeated
    // here.
    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (target == session.GetHabbo())
        {
            session.SendWhisper("You cannot slap yourself.");
            return Task.CompletedTask;
        }

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser == null)
        {
            session.SendWhisper($"{target.Username} is not in this room.");
            return Task.CompletedTask;
        }

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (thisUser == null)
            return Task.CompletedTask;

        var habbo = session.GetHabbo();

        // Restrained: cuffs stop you slapping anywhere, safe zone or not. A
        // passive player may still throw the harmless version below, because
        // passive is protection you chose - cuffs are restraint put on you.
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

        // A slap only does anything where fighting is allowed; inside a safe
        // zone it stays the harmless gesture it has always been.
        var hurts = !room.IsSafeZone;
        if (hurts)
        {
            // Passive and health both live in user_rp_stats.
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
        }

        if (_lastSlap.TryGetValue(session.GetHabbo().Id, out var last))
        {
            var elapsed = (DateTime.UtcNow - last).TotalSeconds;
            if (elapsed < CooldownSeconds)
            {
                // Counts DOWN the seconds still to wait: [5/5] the instant you
                // retry, [1/5] with under a second to go. Ceiling stops it ever
                // reading [0/5] while the gate is still shut.
                var remaining = (int)Math.Ceiling(CooldownSeconds - elapsed);
                session.SendWhisper($"Cooldown [{remaining}/{CooldownSeconds}]");
                return Task.CompletedTask;
            }
        }

        // The slapper's own tile plus the four sharing an edge with it, exactly
        // as :hit reaches. Not :push's wider 3x3 any more - the diagonals are
        // out of reach for both attacks.
        if ((Math.Abs(targetUser.X - thisUser.X) + Math.Abs(targetUser.Y - thisUser.Y)) > 1)
        {
            session.SendWhisper($"Oops, {target.Username} is not close enough.");
            return Task.CompletedTask;
        }

        // Leading AND trailing "*" matter: the client only treats a style-4
        // bubble as an action when the text is wrapped in them, and it then
        // moves the opening marker ahead of the actor's name, rendering
        // "*Actor slaps Target across the face*".
        //
        // Only a slap that lands says what it cost: in a safe zone it is pure
        // flavour and nobody loses health, so claiming damage there would be a
        // lie. Worded like :hit's hit line, which the same reader sees.
        _lastSlap[session.GetHabbo().Id] = DateTime.UtcNow;
        room.SendPacket(new ChatComposer(thisUser.VirtualId, hurts
            ? $"*slaps {target.Username} across the face, causing {Damage} damage*"
            : $"*slaps {target.Username} across the face*", 0, FightBubble));

        if (!hurts)
            return Task.CompletedTask;

        target.RpHealth = Math.Max(0, target.RpHealth - Damage);
        target.SaveRpStats();
        habbo.RpAggression = AggressionOnSlap;
        SendStats(room, targetUser, target);
        SendStats(room, thisUser, habbo);

        if (target.RpHealth <= 0)
            room.GetRoomUserManager().ApplyRpKnockout(targetUser);

        return Task.CompletedTask;
    }

    private static void SendStats(Room room, RoomUser user, Habbo habbo) =>
        room.SendPacket(new RpStatsComposer(user.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax,
            (int)Math.Round(habbo.RpAggression), habbo.IsRpPassive ? 1 : 0, habbo.Rank >= 5 ? 1 : 0));
}
