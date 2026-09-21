using System.Collections.Concurrent;
using System.Drawing;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
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
/// The escort registry since grew a SECOND user that is not police at all: a
/// paramedic carrying an unconscious patient (<see cref="EscortKind"/>). It
/// lives here, under a police-sounding name, because the mechanism is the same
/// one - the same two dictionaries, the same shadow pairing, and above all the
/// same teardown paths. Splitting it out would buy a better name and cost the
/// guarantee that every way a player can vanish still unwinds every escort.
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

    /// <summary>
    /// The ambulance a medical escort puts on BOTH players (Carambulance, per
    /// nitro/overrides/gamedata/EffectMap.json). Custody escorts have no visual
    /// at all; a medical transport is meant to read across the room as an
    /// emergency, from either end of it.
    /// </summary>
    public const int AmbulanceEffectId = 20;

    /// <summary>
    /// Why an escort is running, which is the whole difference between the two.
    ///
    /// <see cref="Custody"/> is the police one: a cuffed, CONSCIOUS suspect
    /// marched by an officer. <see cref="Medical"/> is its mirror: a patient who
    /// is out cold, carried by a paramedic, with no cuffs involved. The
    /// preconditions invert, and so does what ending it has to put back.
    ///
    /// Both live in the SAME registry below rather than in parallel ones. Those
    /// two dictionaries are what every teardown path already unwinds - room
    /// leave, the cycle sweep, disconnect, and the V2 registry's shadow unlink -
    /// and a second pair would mean each of those paths quietly growing a second
    /// call that is easy to miss and impossible to notice the absence of.
    /// </summary>
    public enum EscortKind
    {
        Custody,
        Medical
    }

    /// <summary>Stunned player id -> when the freeze lifts.</summary>
    private static readonly ConcurrentDictionary<int, DateTime> Stunned = new();

    /// <summary>Cuffed player ids. The value is unused - this is a set.</summary>
    private static readonly ConcurrentDictionary<int, bool> Cuffed = new();

    /// <summary>Captor -> suspect, and the reverse, for an active escort.</summary>
    private static readonly ConcurrentDictionary<int, int> EscortByCaptor = new();
    private static readonly ConcurrentDictionary<int, int> EscortBySuspect = new();

    /// <summary>Captor -> why their escort is running. Absent means Custody.</summary>
    private static readonly ConcurrentDictionary<int, EscortKind> KindByCaptor = new();

    /// <summary>
    /// Player id -> the enable they were wearing before a medical escort put an
    /// ambulance on them, so ending one puts back what they had rather than
    /// stripping them to nothing. Written for both parties, cleared by EndEscort.
    /// </summary>
    private static readonly ConcurrentDictionary<int, int> EffectBeforeEscort = new();

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
    /// Is this captor running a medical transport rather than an arrest?
    /// False for no escort at all, so callers can ask without checking first.
    /// </summary>
    public static bool IsMedicalEscort(int captorId) =>
        KindByCaptor.TryGetValue(captorId, out var kind) && kind == EscortKind.Medical;

    /// <summary>
    /// Take a suspect into custody. From here the suspect does not walk: the
    /// movement engine makes them the captor's shadow, so every step the
    /// captor takes is mirrored onto them one tile in front, facing the same
    /// way, on the same beat (<see cref="MovementV2Bridge.Pair"/>). False when
    /// either side is already in an escort or the pair could not be made.
    /// </summary>
    public static bool StartEscort(Room room, RoomUser captor, RoomUser suspect, EscortKind kind = EscortKind.Custody)
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
            KindByCaptor[captorId] = kind;
            suspect.CanWalk = false;
            suspect.UpdateNeeded = true;
            if (kind == EscortKind.Medical)
            {
                // The patient is out cold and therefore laid out on the floor.
                // Being carried is the one thing that takes them off it: the
                // pose lifts for the trip and comes back when they are put
                // down. RpKnockedOut and CanWalk are NOT touched - they are
                // still unconscious and still cannot walk, they are just on a
                // stretcher rather than on the pavement.
                LiftKnockoutPose(suspect);
                WearAmbulance(captor);
                WearAmbulance(suspect);
            }
            return true;
        }
    }

    /// <summary>
    /// Put the ambulance on one player, remembering what they had on first.
    ///
    /// Goes through ApplyEffect, so Effects.CurrentEffect really becomes 20,
    /// rather than sending the composer straight to the room the way the "67"
    /// gesture does (ChatEvent). That distinction is load-bearing: while the
    /// slot still reads 0, UpdatePassiveEffect stamps the passive enable over
    /// the top of it on the very next room tick, because it asserts whenever
    /// the slot is free. Owning the slot is what keeps the ambulance on screen.
    /// </summary>
    private static void WearAmbulance(RoomUser user)
    {
        var effects = user?.GetClient()?.GetHabbo()?.Effects;
        if (effects == null)
            return;
        EffectBeforeEscort[user.UserId] = effects.CurrentEffect;
        user.ApplyEffect(AmbulanceEffectId);
    }

    /// <summary>
    /// Take the ambulance off and give back whatever was underneath it.
    ///
    /// Only if it is still ours, the guard <see cref="Release"/> uses for the
    /// stun visual: something else may have taken the slot in the meantime (a
    /// swim tile, a mount, a fresh :enable), and an escort ending is no reason
    /// to wipe it.
    /// </summary>
    private static void RestoreEffect(RoomUser user)
    {
        if (user == null)
            return;
        if (!EffectBeforeEscort.TryRemove(user.UserId, out var previous))
            return;
        var effects = user.GetClient()?.GetHabbo()?.Effects;
        if (effects == null || effects.CurrentEffect != AmbulanceEffectId)
            return;
        user.ApplyEffect(previous);
    }

    /// <summary>
    /// Take a knocked-out player off the floor for the length of a transport.
    /// The inverse of the lift in RoomUser.UpdateRpKnockoutState, including the
    /// 0.35 the lay owes back - UpdateUserStatus recomputes Z against the tile
    /// only for a unit that is not lying, so the flag and the status have to
    /// move together or the avatar's height stops being reconciled.
    /// </summary>
    private static void LiftKnockoutPose(RoomUser user)
    {
        if (user == null || !user.Statusses.ContainsKey("lay"))
            return;
        user.Statusses.Remove("lay");
        user.Z += 0.35;
        user.IsLying = false;
        user.UpdateNeeded = true;
    }

    /// <summary>
    /// Lay a patient back down, if they are still out cold when they are put
    /// down. Someone healed mid-transport is left standing: their health no
    /// longer holds the pose, and forcing them back onto the floor would undo a
    /// revive that has already happened.
    /// </summary>
    private static void RestoreKnockoutPose(RoomUser user)
    {
        if (user == null || user.Statusses.ContainsKey("lay"))
            return;
        var habbo = user.GetClient()?.GetHabbo();
        if (habbo == null || habbo.RpHealth > 0)
            return;
        if (user.RotBody % 2 != 0)
            user.RotBody--;
        user.RotHead = user.RotBody;
        user.Statusses["lay"] = "1.0 null";
        user.Z -= 0.35;
        user.IsLying = true;
        user.UpdateNeeded = true;
    }

    /// <summary>
    /// End an escort, from either side. Breaks the shadow link (the suspect
    /// comes to rest where their last mirrored step ended) and hands walking
    /// back unless something else holds it. Returns the suspect's id, or 0
    /// when there was no escort to end. Either user may already have left.
    /// </summary>
    public static int EndEscort(Room? room, int captorId, RoomUser? suspectUser, bool restorePose = true)
    {
        lock (EscortSync)
        {
            if (!EscortByCaptor.TryRemove(captorId, out var suspectId))
                return 0;
            EscortBySuspect.TryRemove(suspectId, out _);
            KindByCaptor.TryRemove(captorId, out var kind);

            var manager = room?.GetRoomUserManager();
            var captorUser = manager?.GetRoomUserByHabbo(captorId);
            suspectUser ??= manager?.GetRoomUserByHabbo(suspectId);
            MovementV2Bridge.Unpair(room, captorUser, suspectUser);

            if (kind == EscortKind.Medical)
            {
                // Put the patient down first, then take the ambulances off
                // both. Either RoomUser may already be gone - a disconnect gets
                // here with one side unresolvable - and every step below is a
                // no-op on null, so what CAN be put back is.
                // Not when the caller is about to put them somewhere that
                // supplies its own pose: a bed's lay comes from the furni and
                // is refused for anyone already flagged as lying
                // (UpdateUserStatus returns early), so laying them on the floor
                // first is what would STOP them lying on the bed.
                if (restorePose)
                    RestoreKnockoutPose(suspectUser);
                RestoreEffect(suspectUser);
                RestoreEffect(captorUser);
                // The snapshot is keyed by player id and outlives the RoomUser,
                // so a party who has already left is dropped explicitly rather
                // than left to sit in the dictionary for the emulator's uptime.
                EffectBeforeEscort.TryRemove(suspectId, out _);
                EffectBeforeEscort.TryRemove(captorId, out _);
            }

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

    // ---- drop-off -----------------------------------------------------------

    /// <summary>
    /// How much worse an occupied bed is than an empty one, in squared tiles.
    /// Bigger than any distance a room can hold (the largest model is well
    /// under 1000x1000), so a free bed anywhere beats a taken one next door -
    /// but a taken bed is still better than nothing, which is why this is a
    /// penalty and not a filter.
    /// </summary>
    private const long OccupiedBedPenalty = 1_000_000;

    /// <summary>
    /// Somewhere a patient can be laid down. The hotel's two laying types, the
    /// same pair ItemFunctionOverrides.IsLayingType names - laying is derived
    /// client-side from the interaction type, so this list is not ours to
    /// extend on the server alone.
    /// </summary>
    private static bool IsLayable(Item item) =>
        item?.Definition != null && InteractionTypes.IsLayingSurface(item.Definition.InteractionType);

    /// <summary>
    /// How much better a real medical bed is than any other thing you can lie
    /// on. Large enough to outrank distance outright: a ward's own bed is the
    /// point of the trip, and a sofa nearer the door is not a substitute for
    /// it. Still a score rather than a filter, so a room with no medical bed
    /// falls back to whatever it does have.
    /// </summary>
    private const long NonMedicalBedPenalty = 10_000_000;

    /// <summary>
    /// The bed to use, measured from the DROP-OFF PAD rather than from the
    /// patient: the pad is the fixed thing a hospital lays out its ward around,
    /// and the patient is wherever the last step happened to leave them.
    ///
    /// An occupied bed is not refused, only heavily penalised. PixelRP has
    /// global tile overlap, so two people in one bed is legal and nothing else
    /// would prevent it - but a ward with a free bed should never fill an
    /// occupied one first.
    /// </summary>
    private static Item? NearestLayable(Room room, Item pad)
    {
        var items = room.GetRoomItemHandler()?.GetFloor;
        if (items == null)
            return null;
        var map = room.GetGameMap();
        Item? best = null;
        var bestScore = long.MaxValue;
        foreach (var item in items)
        {
            if (!IsLayable(item))
                continue;
            long dx = item.GetX - pad.GetX;
            long dy = item.GetY - pad.GetY;
            var score = dx * dx + dy * dy;
            if (item.Definition.InteractionType != InteractionType.MedicalBed)
                score += NonMedicalBedPenalty;
            if (map != null && map.MapGotUser(new Point(item.GetX, item.GetY)))
                score += OccupiedBedPenalty;
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = item;
        }
        return best;
    }

    /// <summary>
    /// How a drop-off attempt turned out. Three outcomes rather than a bool,
    /// because the two callers want different things from the middle one: the
    /// pad a medic walks onto keeps them carrying the patient and says why,
    /// while :escort used as a drop puts the patient down regardless and only
    /// wants to know whether a bed was involved.
    /// </summary>
    public enum DropOffResult
    {
        /// <summary>Nothing to put down - not a medical escort, or the patient is gone.</summary>
        NotCarrying,

        /// <summary>Carrying somebody, but the room has nothing to lay them on.</summary>
        NoBed,

        /// <summary>Done: the patient is on a bed and the transport has ended.</summary>
        LaidOnBed
    }

    /// <summary>
    /// The drop-off pad under a unit, or null. Cheap enough to ask per command;
    /// the walk-on path already has the pad in hand and does not use this.
    /// </summary>
    public static Item? DropoffPadUnder(Room room, RoomUser user)
    {
        var items = room?.GetGameMap()?.GetAllRoomItemForSquare(user?.X ?? 0, user?.Y ?? 0);
        if (user == null || items == null)
            return null;
        foreach (var item in items)
        {
            if (item?.Definition != null && item.Definition.InteractionType == InteractionType.ParamedicDropoff)
                return item;
        }
        return null;
    }

    /// <summary>
    /// A paramedic is on a drop-off pad while carrying somebody: lay the
    /// patient on the nearest bed and end the transport.
    ///
    /// Gated on the ESCORT, not on the job. Only an on-duty paramedic can have
    /// started a medical escort in the first place, so the dictionary probe
    /// already answers "is this a qualified medic" - and the walk-on caller
    /// runs this on every step onto the pad, where re-asking the database would
    /// be a query per tile. An officer marching a suspect across the same pad
    /// finds it inert.
    ///
    /// Note the patient crosses the pad BEFORE their medic does: a shadow is
    /// staged one tile in front. That step is inert for the same reason - the
    /// patient is nobody's captor - and the drop-off fires on the beat the
    /// medic themselves arrives.
    /// </summary>
    public static DropOffResult TryDropOff(Room room, RoomUser captor, Item pad)
    {
        if (room == null || captor == null || pad == null || captor.IsBot)
            return DropOffResult.NotCarrying;
        if (!IsMedicalEscort(captor.UserId))
            return DropOffResult.NotCarrying;
        var patientId = SuspectOf(captor.UserId);
        if (patientId == 0)
            return DropOffResult.NotCarrying;
        var manager = room.GetRoomUserManager();
        var patient = manager?.GetRoomUserByHabbo(patientId);
        if (patient == null)
            return DropOffResult.NotCarrying;

        var bed = NearestLayable(room, pad);
        if (bed == null)
            return DropOffResult.NoBed;

        // ORDER IS THE WHOLE TRICK, and it is not interchangeable:
        //
        //   1. put the patient on the bed
        //   2. tell V2 that is where they are (Relocate), so the walk-end that
        //      Unpair stages rests on the bed instead of dragging them back to
        //      the tile the escort last stepped to
        //   3. end the escort WITHOUT the floor pose - the bed supplies its own
        //   4. let UpdateUserStatus read the square and apply the bed's lay
        room.GetGameMap().TeleportToItem(patient, bed);
        MovementV2Bridge.Relocate(room, patient, patient.X, patient.Y, patient.Z);
        EndEscort(room, captor.UserId, patient, restorePose: false);
        manager.UpdateUserStatus(patient, false);
        patient.UpdateNeeded = true;
        return DropOffResult.LaidOnBed;
    }

    /// <summary>
    /// Put the patient down deliberately, which is what :escort does when the
    /// medic is already carrying the person they named.
    ///
    /// On a drop-off pad with a bed to reach, they go on the bed. ANYWHERE
    /// ELSE - off the pad, or on a pad in a room with no bed - the transport
    /// simply ends and they lie back down where they are. A drop command that
    /// refuses to drop would leave a medic stuck carrying somebody with no way
    /// to let go but :unescort, which is the thing this exists to replace.
    /// </summary>
    public static DropOffResult PutDown(Room room, RoomUser captor, RoomUser patient)
    {
        if (room == null || captor == null)
            return DropOffResult.NotCarrying;
        var pad = DropoffPadUnder(room, captor);
        var result = pad == null ? DropOffResult.NotCarrying : TryDropOff(room, captor, pad);
        if (result == DropOffResult.LaidOnBed)
            return result;
        // The ordinary release: EndEscort lays them back down where they stand,
        // because they are still out cold and nothing here supplies a pose.
        EndEscort(room, captor.UserId, patient);
        return result == DropOffResult.NoBed ? DropOffResult.NoBed : DropOffResult.NotCarrying;
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
    ///
    /// EXCEPT a medical transport whose patient is the one who went down -
    /// being out cold is the entire premise of that escort, not a reason to
    /// abandon it. A medic who goes down still drops whoever they were
    /// carrying, and every custody escort still ends either way.
    /// </summary>
    public static void OnKnockout(Room room, RoomUser user)
    {
        if (room == null || user == null || (EscortByCaptor.IsEmpty && EscortBySuspect.IsEmpty))
            return;
        if (IsEscorting(user.UserId))
            EndEscort(room, user.UserId, null);
        var captorId = CaptorOf(user.UserId);
        if (captorId != 0 && !IsMedicalEscort(captorId))
            EndEscort(room, captorId, user);
    }

    /// <summary>
    /// Somebody has just been brought back above zero health. A medical
    /// transport ends there and then: the premise is gone, and
    /// UpdateRpKnockoutState has already handed them CanWalk back. Leaving the
    /// pair standing would pin a player who is free to walk to a shadow that
    /// refuses every click they make (MovementV2Bridge.RequestMove), which
    /// reads as a frozen avatar with nothing visibly holding it.
    ///
    /// A custody escort is untouched: waking up is not release.
    /// </summary>
    public static void OnRevive(Room room, RoomUser user)
    {
        if (room == null || user == null || EscortBySuspect.IsEmpty)
            return;
        var captorId = CaptorOf(user.UserId);
        if (captorId != 0 && IsMedicalEscort(captorId))
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
