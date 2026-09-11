using Plus.HabboHotel.Corporations;
using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police actions: :stun - freeze a nearby target for a few seconds.
///
/// Ported from the old Arcturus plugin. Its clocked-in-officer gate is back:
/// only an on-duty employee of a corporation flagged `is_police` can fire
/// (PoliceUtility). The stungun-charge gate it also had is still not here -
/// there is no weapon inventory to draw from yet.
///
/// Reach is DIRECTIONAL, which is what makes this different from every other
/// combat command in the hotel. Along a straight grid line - same row or same
/// column - it reaches two tiles, so you can stand one clear tile back. On a
/// true diagonal it reaches one. Any other offset never connects at all: a
/// knight's-move target is out of the firing line however close it looks.
///
/// A pull of the trigger is a pull of the trigger: out of reach is a public
/// miss, not a private "too far away", and it costs the cooldown and turns the
/// shooter aggressive just as a landed stun does.
/// </summary>
internal class StunCommand : ITargetChatCommand
{
    public string Key => "stun";
    public string PermissionRequired => "command_stun";

    public string Parameters => "%target%";

    public string Description => "Stun another user, freezing them briefly.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "No target selected.";

    /// <summary>Blue bubble, the one every combat action shares.</summary>
    private const int FightBubble = 4;

    /// <summary>How far a shot carries along a row or column.</summary>
    private const int StraightReach = 2;

    /// <summary>Seconds the target stays frozen.</summary>
    private const int StunSeconds = 3;

    /// <summary>
    /// Seconds between shots. The stun gun keeps its OWN timer, longer than
    /// the punching cooldown and independent of it, so neither blocks the
    /// other - as in the original.
    /// </summary>
    private const int CooldownSeconds = 4;

    private const int AggressionOnShot = 100;

    private readonly ConcurrentDictionary<int, DateTime> _lastShot = new();

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        // Police powers are a job: on the force AND clocked in.
        if (!PoliceUtility.RequireOnDuty(session, "fire a stun gun"))
            return Task.CompletedTask;

        // pixelrp: never on one of your own characters - see ChargeCommand.
        if (AccountUtility.SameAccount(habbo.Id, target.Id))
        {
            session.SendWhisper("That is one of your own characters.");
            return Task.CompletedTask;
        }
        if (target == habbo)
        {
            session.SendWhisper("You cannot stun yourself.");
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

        // In someone else's custody you are in no position to shoot. Checked
        // first so an escorted player hears about the escort rather than
        // something less useful.
        if (PoliceState.IsBeingEscorted(habbo.Id))
        {
            session.SendWhisper("You cannot do that while you are being escorted.");
            return Task.CompletedTask;
        }

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

        // Freezing someone who is already on the floor does nothing, and the
        // stun's timed release would end up fighting the knockout over who
        // owns their ability to walk.
        if (target.RpHealth <= 0)
        {
            session.SendWhisper($"{target.Username} is already out cold.");
            return Task.CompletedTask;
        }

        // Likewise someone already in custody: the escort owns their movement
        // outright, so a stun could neither freeze them nor add anything.
        if (PoliceState.IsBeingEscorted(target.Id))
        {
            session.SendWhisper($"{target.Username} is already in custody.");
            return Task.CompletedTask;
        }

        if (_lastShot.TryGetValue(habbo.Id, out var last))
        {
            var elapsed = (DateTime.UtcNow - last).TotalSeconds;
            if (elapsed < CooldownSeconds)
            {
                var remaining = (int)Math.Ceiling(CooldownSeconds - elapsed);
                session.SendWhisper($"Cooldown [{remaining}/{CooldownSeconds}]");
                return Task.CompletedTask;
            }
        }

        // The trigger is pulled from here on, hit or miss.
        _lastShot[habbo.Id] = DateTime.UtcNow;
        habbo.RpAggression = AggressionOnShot;

        if (InReach(thisUser, targetUser))
        {
            PoliceState.Stun(room, targetUser, StunSeconds);
            room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*stuns {target.Username}, freezing them in place*", 0, FightBubble));
        }
        else
            room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*uses their stun gun on {target.Username}, but misses*", 0, FightBubble));

        PoliceState.SendStats(room, thisUser, habbo);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The firing line: straight along a row or column for two tiles, one tile
    /// on a true diagonal, nothing off those lines. The shared tile counts, as
    /// it does for a punch.
    /// </summary>
    private static bool InReach(RoomUser shooter, RoomUser target)
    {
        var dx = Math.Abs(target.X - shooter.X);
        var dy = Math.Abs(target.Y - shooter.Y);
        if (dx == 0 && dy == 0)
            return true;
        if (dx == 0 || dy == 0)
            return Math.Max(dx, dy) <= StraightReach;
        if (dx == dy)
            return dx == 1;
        return false;
    }
}
