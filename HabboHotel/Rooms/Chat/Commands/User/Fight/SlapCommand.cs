using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.Utilities;

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

    public bool IsRanged => true;

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
    /// Each player's slap cooldown (PlayerCooldown, which forgives a slow
    /// connection). Only successful slaps start it, so one that missed for range
    /// costs nothing.
    /// </summary>
    private readonly PlayerCooldown _cooldown = new(CooldownSeconds);

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
            session.SendWhisper(RangeMessages.NotInRoom);
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

            if (habbo.CannotFight)
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
                session.SendWhisper(KnockedOut.TargetRefusal);
                return Task.CompletedTask;
            }
        }

        if (_cooldown.IsWaiting(session.GetHabbo().Id, out var secondsLeft))
        {
            session.SendWhisper(_cooldown.Refusal(secondsLeft));
            return Task.CompletedTask;
        }

        // The slapper's own tile plus the four sharing an edge with it, exactly
        // as :hit reaches. Not :push's wider 3x3 any more - the diagonals are
        // out of reach for both attacks.
        if ((Math.Abs(targetUser.X - thisUser.X) + Math.Abs(targetUser.Y - thisUser.Y)) > 1)
        {
            session.SendWhisper(RangeMessages.TooFar(target.Username));
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
        _cooldown.Start(session.GetHabbo().Id);
        room.SendPacket(new ChatComposer(thisUser.VirtualId, hurts
            ? $"*slaps {target.Username} across the face, causing {Damage} damage*"
            : $"*slaps {target.Username} across the face*", 0, FightBubble));
        // Slapped, hurt or not: an officer's trip to a call is off.
        Plus.HabboHotel.Corporations.EmergencyCalls.CancelTrip(target.Id);

        if (!hurts)
            return Task.CompletedTask;

        // pixelrp durability: worn armour wears by the slap and soaks its share (RpDurability).
        target.RpHealth = Math.Max(0, target.RpHealth - RpDurability.Absorb(target, Damage));
        // pixelrp: so does a slap - it is a hit, if a light one.
        BinSearch.Interrupt(target.Id);
        // Queued (RoomTickWriter), not written here: the reply no longer waits on the database.
        RoomTickWriter.QueueRpStats(target);
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
