using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police actions: the runtime state behind :stun, :cuff and :escort.
///
/// Ported from the old Arcturus plugin (StunManager, CuffManager,
/// PoliceEscortManager), collapsed into one place because all three are the
/// same shape - a session-only registry keyed by player id - and because they
/// interlock: a cuff is only possible on a stunned target, an escort only on a
/// cuffed one, and each has to know what the others own.
///
/// Nothing is persisted. Everything here dies with the emulator, which is what
/// the original did too: being stunned or cuffed is a moment, not a property of
/// an account.
///
/// The one deliberate departure from the original: there are no scheduled
/// tasks. Arcturus armed a ScheduledFuture per stun and ran the escort off its
/// own 250ms timer; pixelrp already ticks every room at 500ms and already
/// drives the knockout, passive and aggression clocks from there, so the stun
/// clock rides the room tick (<see cref="TickStun"/>) and the escort drag rides
/// the step the captor actually takes (<see cref="DragSuspect"/>). No timer to
/// cancel, nothing to leak, and no second thread touching a RoomUser.
/// </summary>
public static class PoliceState
{
    /// <summary>
    /// Stand-in for the original's stun visual. It used effect 53, but 53 in
    /// this hotel's EffectMap is Easterchicks - the two projects ship
    /// different effect sets, so the id could not travel with the code. 236 is
    /// CompletelyConfused, the closest thing here to being tased.
    /// </summary>
    public const int StunEffectId = 236;

    /// <summary>
    /// The cuff has NO visual yet. The original rendered a custom overhead
    /// handcuff (effect 1000, lib OverheadCuff) built from PNG frames by its
    /// own build-effect-nitro.py; that bundle is not in this project and its
    /// source frames are no longer in that one either. Cuffed players read as
    /// cuffed from the emote and from being unable to fight.
    /// </summary>
    public const int NoEffectId = 0;

    /// <summary>Stunned player id -> when the freeze lifts.</summary>
    private static readonly ConcurrentDictionary<int, DateTime> Stunned = new();

    /// <summary>Cuffed player ids. The value is unused - this is a set.</summary>
    private static readonly ConcurrentDictionary<int, bool> Cuffed = new();

    /// <summary>Captor -> suspect, and the reverse, for an active escort.</summary>
    private static readonly ConcurrentDictionary<int, int> EscortByCaptor = new();
    private static readonly ConcurrentDictionary<int, int> EscortBySuspect = new();

    // ---- stun --------------------------------------------------------------

    public static bool IsStunned(int habboId) => Stunned.ContainsKey(habboId);

    /// <summary>
    /// Freeze a player where they stand for a few seconds. Re-stunning
    /// REFRESHES the window rather than stacking, exactly as the original did.
    /// </summary>
    public static void Stun(Room room, RoomUser user, int seconds)
    {
        if (room == null || user == null || user.IsBot)
            return;
        Stunned[user.UserId] = DateTime.UtcNow.AddSeconds(seconds);
        // Halt an in-flight walk on the spot instead of letting it finish the
        // path, then block new clicks for the duration.
        user.ClearMovement(true);
        user.CanWalk = false;
        user.ApplyEffect(StunEffectId);
        user.UpdateNeeded = true;
    }

    /// <summary>
    /// Lifts one player's stun once its time is up. Called from the room tick
    /// beside the aggression drain, so a freeze ends within half a second of
    /// expiring and no separate timer has to exist.
    /// </summary>
    public static void TickStun(RoomUser user)
    {
        if (user == null || user.IsBot || Stunned.IsEmpty)
            return;
        if (!Stunned.TryGetValue(user.UserId, out var until) || DateTime.UtcNow < until)
            return;
        Stunned.TryRemove(user.UserId, out _);
        Release(user);
    }

    /// <summary>
    /// End a stun early - what starting an escort on a stunned suspect does,
    /// so the drag can move them immediately instead of waiting out the freeze.
    /// </summary>
    public static void CancelStun(RoomUser user)
    {
        if (user == null || !Stunned.TryRemove(user.UserId, out _))
            return;
        Release(user);
    }

    /// <summary>
    /// Hand walking back, unless something else owns the flag. A knocked-out
    /// player is frozen by their own health and an escorted suspect is pinned
    /// by their captor; restoring CanWalk for either would let them walk away
    /// from a state they are supposed to be stuck in.
    /// </summary>
    private static void Release(RoomUser user)
    {
        var habbo = user.GetClient()?.GetHabbo();
        var down = habbo != null && habbo.RpHealth <= 0;
        if (!down && !IsBeingEscorted(user.UserId))
            user.CanWalk = true;
        // Only clear the visual if it is still ours - nothing else applies 236
        // today, but a later enable would otherwise be wiped by a stun ending.
        if (habbo?.Effects != null && habbo.Effects.CurrentEffect == StunEffectId)
            user.ApplyEffect(NoEffectId);
        user.UpdateNeeded = true;
    }

    // ---- cuffs -------------------------------------------------------------

    public static bool IsCuffed(int habboId) => Cuffed.ContainsKey(habboId);

    /// <summary>Cuff a player. False when they already were.</summary>
    public static bool Cuff(int habboId) => Cuffed.TryAdd(habboId, true);

    /// <summary>Uncuff a player. False when they were not cuffed.</summary>
    public static bool Uncuff(int habboId) => Cuffed.TryRemove(habboId, out _);

    // ---- escort ------------------------------------------------------------

    public static bool IsBeingEscorted(int suspectId) => EscortBySuspect.ContainsKey(suspectId);

    public static bool IsEscorting(int captorId) => EscortByCaptor.ContainsKey(captorId);

    public static int SuspectOf(int captorId) => EscortByCaptor.TryGetValue(captorId, out var id) ? id : 0;

    public static int CaptorOf(int suspectId) => EscortBySuspect.TryGetValue(suspectId, out var id) ? id : 0;

    /// <summary>
    /// Take a suspect into custody. The suspect stops being able to walk for
    /// themselves - from here their movement is whatever the captor's steps
    /// give them. False when either side is already in an escort.
    /// </summary>
    public static bool StartEscort(int captorId, int suspectId, RoomUser suspectUser)
    {
        if (captorId == suspectId)
            return false;
        if (IsEscorting(captorId) || IsBeingEscorted(suspectId) || IsEscorting(suspectId) || IsBeingEscorted(captorId))
            return false;
        if (!EscortByCaptor.TryAdd(captorId, suspectId))
            return false;
        if (!EscortBySuspect.TryAdd(suspectId, captorId))
        {
            EscortByCaptor.TryRemove(captorId, out _);
            return false;
        }
        if (suspectUser != null)
        {
            suspectUser.ClearMovement(true);
            suspectUser.CanWalk = false;
            suspectUser.UpdateNeeded = true;
        }
        return true;
    }

    /// <summary>
    /// End an escort, from either side. Returns the suspect's id, or 0 when
    /// there was no escort to end.
    /// </summary>
    public static int EndEscort(int captorId, RoomUser suspectUser)
    {
        if (!EscortByCaptor.TryRemove(captorId, out var suspectId))
            return 0;
        EscortBySuspect.TryRemove(suspectId, out _);
        if (suspectUser != null)
        {
            // A suspect who is knocked out or still stunned keeps standing
            // still - those states own the flag and clear it themselves.
            var habbo = suspectUser.GetClient()?.GetHabbo();
            var down = habbo != null && habbo.RpHealth <= 0;
            if (!down && !IsStunned(suspectId))
                suspectUser.CanWalk = true;
            suspectUser.UpdateNeeded = true;
        }
        return suspectId;
    }

    /// <summary>
    /// How far behind the suspect may fall before they are put down instead of
    /// walked. Reached only when something moved them without walking them - a
    /// roller, a teleport, a door - where walking back would mean a long
    /// pathfind across the room.
    /// </summary>
    private const int SnapDistance = 4;

    /// <summary>
    /// March the suspect along with the captor's step. They are driven onto the
    /// tile one PAST the captor in the direction the captor is facing, so they
    /// are shoved along in front rather than trailing behind, and they are
    /// turned to face the captor's way whether or not they moved.
    ///
    /// They are WALKED, not placed. MoveTo routes into Movement V2, which emits
    /// the timed edge records the client interpolates - that is what makes this
    /// a walk on screen, with the walking posture and animation, instead of the
    /// jump a SetPos gives. One tile at a time, so this is not a route being
    /// planned across the room: it is the same single step the captor just took.
    ///
    /// V2 never consults CanWalk, which is what lets a suspect who cannot walk
    /// for themselves still be driven.
    ///
    /// LOCK ORDER: this runs under RoomUserManager._cycleLock and MoveTo takes
    /// the room's MovementLock, so the order here is _cycleLock then
    /// MovementLock. That is safe only because nothing goes the other way - the
    /// scheduler holds MovementLock and never touches _cycleLock, and the Q1
    /// outbound worker takes _cycleLock without holding MovementLock. Keep it
    /// that way.
    /// </summary>
    public static void DragSuspect(Room room, RoomUser captor)
    {
        if (room == null || captor == null || captor.IsBot || EscortByCaptor.IsEmpty)
            return;
        if (!EscortByCaptor.TryGetValue(captor.UserId, out var suspectId))
            return;
        var suspect = room.GetRoomUserManager().GetRoomUserByHabbo(suspectId);
        if (suspect == null || suspect.IsBot)
            return;

        var map = room.GetGameMap();
        var dx = RotationX(captor.RotBody);
        var dy = RotationY(captor.RotBody);
        var x = captor.X + dx;
        var y = captor.Y + dy;
        // Nowhere to be shoved (no facing, a wall, the edge of the room): the
        // captor's own tile will do - players may share one here.
        if ((dx == 0 && dy == 0) || !map.ValidTile(x, y) || !map.CanWalk(x, y, false))
        {
            x = captor.X;
            y = captor.Y;
        }

        // Facing goes with the captor every time, so a suspect already standing
        // on the right tile still turns when their captor does.
        suspect.RotBody = captor.RotBody;
        suspect.RotHead = captor.RotBody;
        suspect.UpdateNeeded = true;

        if (suspect.X == x && suspect.Y == y)
            return;

        if ((Math.Abs(suspect.X - x) + Math.Abs(suspect.Y - y)) > SnapDistance)
        {
            suspect.ClearMovement(true);
            suspect.SetPos(x, y, map.SqAbsoluteHeight(x, y));
            return;
        }

        // pOverride so a destination that already holds the captor is not
        // refused before the pathfinder is even asked.
        suspect.MoveTo(x, y, true);
    }

    private static int RotationX(int rotation) => rotation switch
    {
        1 or 2 or 3 => 1,
        5 or 6 or 7 => -1,
        _ => 0
    };

    private static int RotationY(int rotation) => rotation switch
    {
        3 or 4 or 5 => 1,
        7 or 0 or 1 => -1,
        _ => 0
    };

    // ---- leaving -----------------------------------------------------------

    /// <summary>
    /// Forget everything about a player who has gone. Called when a user
    /// leaves a room: a stun or a cuff is a moment in a room, and an escort
    /// cannot outlive either party being there.
    /// </summary>
    public static void Forget(int habboId)
    {
        Stunned.TryRemove(habboId, out _);
        Cuffed.TryRemove(habboId, out _);
        if (EscortByCaptor.TryRemove(habboId, out var suspectId))
            EscortBySuspect.TryRemove(suspectId, out _);
        if (EscortBySuspect.TryRemove(habboId, out var captorId))
            EscortByCaptor.TryRemove(captorId, out _);
    }

    /// <summary>Push a player's stats to the room's HUDs after aggression moved.</summary>
    public static void SendStats(Room room, RoomUser user, Habbo habbo) =>
        room.SendPacket(new RpStatsComposer(user.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax,
            (int)Math.Round(habbo.RpAggression), habbo.IsRpPassive ? 1 : 0, habbo.Rank >= 5 ? 1 : 0));
}
