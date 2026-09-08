using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.Rooms.Movement;
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
/// Only ONE of the three runs on a clock: the stun. A cuff lasts until :uncuff
/// and an escort until it is ended, so once a player is cuffed the arrest does
/// not quietly time out from under the officer working it - any player can
/// escort them, for as long as the cuffs are on.
///
/// The one deliberate departure from the original: there are no scheduled
/// tasks. Arcturus armed a ScheduledFuture per stun and ran the escort off its
/// own 250ms timer; pixelrp already ticks every room at 500ms and already
/// drives the knockout, passive and aggression clocks from there, so the stun
/// clock rides the room tick (<see cref="TickStun"/>), and the escort is not a
/// clock at all: the suspect becomes the captor's shadow inside the movement
/// engine (<see cref="MovementV2Bridge.Pair"/>), staged one tile in front on
/// every step the captor takes. No timer to cancel, nothing to leak, and no
/// second thread touching a RoomUser.
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

    /// <summary>
    /// Serialises starting and ending an escort. Each is two steps - the
    /// registry above and the movement link (MovementV2Bridge.Pair/Unpair) -
    /// and commands from different players run on different threads. Without
    /// this, an :uncuff landing between a captor's registration and their
    /// Pair would clear the registry, find no link yet to break, and leave
    /// the pair standing with nothing left that could undo it.
    /// Order: EscortSync -> MovementLock, never the reverse. Callers that
    /// arrive under _cycleLock (RemoveUserFromRoom) keep _cycleLock first.
    /// </summary>
    private static readonly object EscortSync = new();

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
        // path, then block new clicks for the duration. ClearMovement is V1's;
        // the walk itself belongs to V2 and has to be stopped there too.
        // Not for a shadowed suspect: they have no walk of their own, and
        // their "mv" is written by the escort's records each beat - clearing
        // it here would only strip the walking posture for a beat while they
        // keep sliding. (:stun refuses them anyway; this covers any other
        // caller.)
        if (!IsBeingEscorted(user.UserId))
        {
            user.ClearMovement(true);
            MovementV2Bridge.Halt(user);
        }
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
    /// End a stun early. Both of the steps that follow a stun do this, because
    /// each one takes over from it: a cuff going on ends the freeze that made
    /// the cuff possible (:cuff), and an escort starting can then move the
    /// suspect immediately instead of waiting one out (:escort). Nothing about
    /// either state is left depending on a stun that is still ticking.
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
    /// Take a suspect into custody. From here the suspect does not walk: the
    /// movement engine makes them the captor's shadow, so every step the
    /// captor takes is mirrored onto them one tile in front, facing the same
    /// way, on the same beat (<see cref="MovementV2Bridge.Pair"/>). False when
    /// either side is already in an escort or the pair could not be made.
    /// </summary>
    public static bool StartEscort(Room room, RoomUser captor, RoomUser suspect)
    {
        if (room == null || captor == null || suspect == null || captor == suspect)
            return false;
        var captorId = captor.UserId;
        var suspectId = suspect.UserId;
        lock (EscortSync)
        {
            if (IsEscorting(captorId) || IsBeingEscorted(suspectId) || IsEscorting(suspectId) || IsBeingEscorted(captorId))
                return false;
            if (!EscortByCaptor.TryAdd(captorId, suspectId))
                return false;
            if (!EscortBySuspect.TryAdd(suspectId, captorId))
            {
                EscortByCaptor.TryRemove(captorId, out _);
                return false;
            }
            if (!MovementV2Bridge.Pair(room, captor, suspect))
            {
                EscortByCaptor.TryRemove(captorId, out _);
                EscortBySuspect.TryRemove(suspectId, out _);
                return false;
            }
            suspect.CanWalk = false;
            suspect.UpdateNeeded = true;
            return true;
        }
    }

    /// <summary>
    /// End an escort, from either side. Breaks the shadow link (the suspect
    /// comes to rest where their last mirrored step ended) and hands walking
    /// back unless something else holds it. Returns the suspect's id, or 0
    /// when there was no escort to end. Either user may already have left.
    /// </summary>
    public static int EndEscort(Room? room, int captorId, RoomUser? suspectUser)
    {
        lock (EscortSync)
        {
            if (!EscortByCaptor.TryRemove(captorId, out var suspectId))
                return 0;
            EscortBySuspect.TryRemove(suspectId, out _);

            var manager = room?.GetRoomUserManager();
            var captorUser = manager?.GetRoomUserByHabbo(captorId);
            suspectUser ??= manager?.GetRoomUserByHabbo(suspectId);
            MovementV2Bridge.Unpair(room, captorUser, suspectUser);

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
    }

    /// <summary>
    /// The captor turned on the spot (LookTo). The suspect is NOT moved: a turn
    /// is not a step, and an officer looking around or clicking someone across
    /// the room would otherwise drag their captive round with them. All this
    /// does now is keep V2's idea of the captor's facing and tile honest, so
    /// their next real step starts from the truth. Cheap for everyone else:
    /// one dictionary probe on an empty set.
    /// </summary>
    public static void OnCaptorTurn(Room room, RoomUser captor, int rot)
    {
        if (room == null || captor == null || EscortByCaptor.IsEmpty || !EscortByCaptor.ContainsKey(captor.UserId))
            return;
        MovementV2Bridge.Turn(room, captor, (byte)rot);
    }

    /// <summary>
    /// Somebody has just been knocked out. Nobody marches, or is marched,
    /// while out cold: an escort involving them ends. The cuffs stay on.
    /// </summary>
    public static void OnKnockout(Room room, RoomUser user)
    {
        if (room == null || user == null || (EscortByCaptor.IsEmpty && EscortBySuspect.IsEmpty))
            return;
        if (IsEscorting(user.UserId))
            EndEscort(room, user.UserId, null);
        var captorId = CaptorOf(user.UserId);
        if (captorId != 0)
            EndEscort(room, captorId, user);
    }

    // ---- leaving -----------------------------------------------------------

    /// <summary>
    /// Forget everything about a player who has gone. Called as a user leaves
    /// a room, while both RoomUsers are still resolvable: a stun or a cuff is a
    /// moment in a room, and an escort cannot outlive either party being there
    /// - the one staying behind is let go properly.
    ///
    /// Called from EVERY way a player can go, not only the tidy one: the room
    /// leave above, the low-level RemoveRoomUser the room cycle uses to sweep a
    /// user whose session has already died, and the disconnect itself. That
    /// spread is deliberate. These registries are process-global and keyed by
    /// player id, so one missed call does not expire - the player stays cuffed,
    /// unable to throw a punch, for the rest of the emulator's uptime, with
    /// nothing but :uncuff or a restart to clear it. The escort is worse still:
    /// a suspect left in EscortBySuspect can never be handed CanWalk back, and
    /// :unescort only works from the captor's side, who may be long gone.
    /// Overlapping calls cost a few dictionary probes and nothing else.
    ///
    /// <paramref name="room"/> may be null, for a session that has already lost
    /// its room reference. The registries clear either way; only breaking the
    /// movement pair needs a room, and a player with no room has no pair left.
    /// </summary>
    public static void Forget(Room? room, int habboId)
    {
        Stunned.TryRemove(habboId, out _);
        Cuffed.TryRemove(habboId, out _);
        if (IsEscorting(habboId))
            EndEscort(room, habboId, null);
        var captorId = CaptorOf(habboId);
        if (captorId != 0)
            EndEscort(room, captorId, null);
    }

    /// <summary>Push a player's stats to the room's HUDs after aggression moved.</summary>
    public static void SendStats(Room room, RoomUser user, Habbo habbo) =>
        room.SendPacket(new RpStatsComposer(user.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax,
            (int)Math.Round(habbo.RpAggression), habbo.IsRpPassive ? 1 : 0, habbo.Rank >= 5 ? 1 : 0));
}
