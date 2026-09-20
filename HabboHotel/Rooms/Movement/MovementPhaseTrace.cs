using NLog;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: the shared room movement PHASE lifecycle.
/// DIAGNOSTIC ONLY - it reads state and writes log lines, and changes no
/// movement behaviour at all.
///
/// THE INVARIANT IT EXISTS TO TEST:
///
///   The room phase must stay alive while at least one REAL user is Moving or
///   Pending. A joiner stopping or leaving must not reseed it while another
///   real Moving/Pending avatar remains.
///
/// WHAT INSPECTION ALREADY SETTLES, and it narrows the search. `PhaseAnchor`
/// has exactly ONE writer - MovementController.ResolveStartOrigin - and two
/// readers. Nothing clears it: not MovementRegistry.RemoveState, not
/// MovementV2Bridge.OnUserLeave, not StopWalk, not teardown. So the suspected
/// "a leaver CLEARS the phase" cannot happen as stated.
///
/// What CAN happen is a RESEED: `ResolveStartOrigin` overwrites PhaseAnchor
/// with a fresh nowMs whenever `HasLivePhase` says no other real user is
/// Moving or Pending. If that ever answers false while one actually is, the
/// next walker establishes a different phase and the room silently splits into
/// two timelines. That is the failure this hunts, and it is why every
/// Established event is checked against an INDEPENDENT recount rather than
/// trusting the same predicate twice.
///
/// BOUNDED, and off by default. Phase events are rare - one per walk start and
/// one per unit removal - but a hard line cap means an armed session can never
/// become a log flood no matter what goes wrong. Config/nlog.config wraps the
/// console target in an AsyncWrapper, so a write hands off to a background
/// thread and never blocks the caller; that is what keeps this clear of
/// invariant I-5's ban on a blocking sink from the scheduler thread.
/// </summary>
public static class MovementPhaseTrace
{
    private static readonly Logger Log = LogManager.GetLogger("MovementV2");

    /// <summary>Hard ceiling per armed session. Rare events, but bounded regardless.</summary>
    public const int MaxLines = 500;

    private static volatile bool _enabled;
    private static int _lines;
    private static long _violations;

    public static bool Enabled => _enabled;

    public static long Violations => Interlocked.Read(ref _violations);

    public static void Arm()
    {
        Interlocked.Exchange(ref _lines, 0);
        Interlocked.Exchange(ref _violations, 0);
        _enabled = true;
        Log.Info($"[MV2/phase] ARMED maxLines={MaxLines}");
    }

    public static void Disarm()
    {
        _enabled = false;
        Log.Info($"[MV2/phase] DISARMED lines={Interlocked.CompareExchange(ref _lines, 0, 0)} " +
                 $"invariantViolations={Violations}");
    }

    public static string Stats() =>
        $"armed={_enabled} lines={Interlocked.CompareExchange(ref _lines, 0, 0)}/{MaxLines} " +
        $"invariantViolations={Violations}";

    /// <summary>
    /// Count REAL users in each live mode. Excludes <paramref name="self"/> by
    /// reference, exactly as HasLivePhase does, so the two can be compared.
    ///
    /// Deliberately an INDEPENDENT scan rather than a call into HasLivePhase:
    /// checking a predicate against itself proves nothing. Caller holds
    /// MovementLock.
    /// </summary>
    private static void CountReal(
        RoomMovement room, MovementState? self, out int moving, out int pending)
    {
        moving = 0;
        pending = 0;

        foreach (var other in room.States.Values)
        {
            if (self != null && ReferenceEquals(other, self))
                continue;
            if (!other.IsRealUser)
                continue;
            if (other.Mode == MovementMode.Moving)
                moving++;
            else if (other.Mode == MovementMode.Pending)
                pending++;
        }
    }

    private static bool Budget()
    {
        var n = Interlocked.Increment(ref _lines);

        if (n <= MaxLines)
            return true;

        // Report exhaustion exactly once - n is only ever MaxLines + 1 on one
        // call - then go quiet but stay armed, so the violation counter keeps
        // accumulating for :movementphase.
        if (n == MaxLines + 1)
            Log.Info($"[MV2/phase] line cap {MaxLines} reached - still counting violations, no further lines.");

        return false;
    }

    private static long PhaseOf(long anchor)
    {
        var interval = MovementSettings.IntervalMs;
        return ((anchor % interval) + interval) % interval;
    }

    /// <summary>
    /// A walk start resolved against the room phase. Called from StartWalk right
    /// after ResolveStartOrigin, with the anchor as it was BEFORE the call, so
    /// an establish is visible as a change rather than inferred.
    ///
    /// Caller holds MovementLock.
    /// </summary>
    public static void OnWalkStart(
        RoomMovement room, MovementState w, long nowMs, long anchorBefore, long origin)
    {
        if (!_enabled)
            return;

        CountReal(room, w, out var moving, out var pending);
        var active = moving + pending;

        var established = w.LastPhaseDecision == PhaseDecision.Established;

        // THE CHECK. ResolveStartOrigin only establishes when HasLivePhase said
        // no other real user is live. An independent recount saying otherwise
        // means the phase was reseeded out from under a walking avatar - the
        // exact failure this exists to catch.
        var violated = established && active > 0;

        if (violated)
            Interlocked.Increment(ref _violations);

        if (!Budget())
            return;

        var reason = w.LastPhaseDecision switch
        {
            PhaseDecision.Established => anchorBefore == 0 ? "ESTABLISHED_FIRST" : "RESEEDED",
            PhaseDecision.Aligned => w.LastStartDelayMs > 0 ? "REUSED_BY_JOINER" : "REUSED_ON_BOUNDARY",
            PhaseDecision.Skipped => "SKIPPED_TOO_FAR",
            _ => w.IsRealUser ? "NONE" : "BOT_OR_PET_BYPASS"
        };

        var line =
            $"[MV2/phase {reason}] room={room.RoomId} unit={w.VirtualId} " +
            $"mode={w.Mode} isRealUser={w.IsRealUser} " +
            $"oldAnchor={anchorBefore} oldPhase={PhaseOf(anchorBefore)} " +
            $"newAnchor={room.PhaseAnchor} newPhase={PhaseOf(room.PhaseAnchor)} " +
            $"serverNow={nowMs} origin={origin} startDelayMs={w.LastStartDelayMs} " +
            $"realMovingCount={moving} realPendingCount={pending} activeRealMovementCount={active}";

        if (violated)
            Log.Warn($"[MV2/phase INVARIANT_VIOLATION] a phase was reseeded while {active} real " +
                     $"avatar(s) were still Moving/Pending. {line}");
        else
            Log.Info(line);
    }

    /// <summary>
    /// A unit was removed - stop, leave or disconnect. Counts are taken either
    /// side of the removal so "did the last live walker just go" is answered by
    /// the data rather than inferred.
    ///
    /// phaseCleared is expected to be FALSE always: nothing in V2 clears
    /// PhaseAnchor. A true here would itself be the finding.
    ///
    /// Caller holds MovementLock.
    /// </summary>
    public static void OnUnitRemoved(
        RoomMovement room, MovementState removed, long nowMs,
        long anchorBefore, int movingBefore, int pendingBefore)
    {
        if (!_enabled)
            return;

        CountReal(room, null, out var movingAfter, out var pendingAfter);

        var activeBefore = movingBefore + pendingBefore;
        var activeAfter = movingAfter + pendingAfter;
        var cleared = room.PhaseAnchor != anchorBefore;

        // Removing a unit must never touch the anchor, whoever is left.
        if (cleared)
            Interlocked.Increment(ref _violations);

        if (!Budget())
            return;

        var line =
            $"[MV2/phase UNIT_REMOVED] room={room.RoomId} unit={removed.VirtualId} " +
            $"mode={removed.Mode} isRealUser={removed.IsRealUser} " +
            $"oldAnchor={anchorBefore} oldPhase={PhaseOf(anchorBefore)} " +
            $"newAnchor={room.PhaseAnchor} newPhase={PhaseOf(room.PhaseAnchor)} " +
            $"phaseCleared={cleared} serverNow={nowMs} " +
            $"realMovingBefore={movingBefore} realPendingBefore={pendingBefore} " +
            $"activeRealMovementBefore={activeBefore} " +
            $"realMovingAfter={movingAfter} realPendingAfter={pendingAfter} " +
            $"activeRealMovementAfter={activeAfter} " +
            $"phaseStillNeeded={(activeAfter > 0)}";

        if (cleared)
            Log.Warn($"[MV2/phase INVARIANT_VIOLATION] removing a unit changed the room phase " +
                     $"with {activeAfter} real avatar(s) still Moving/Pending. {line}");
        else
            Log.Info(line);
    }

    /// <summary>Counts taken BEFORE a removal, so the caller can pass them through.</summary>
    public static void SampleBefore(RoomMovement room, out int moving, out int pending) =>
        CountReal(room, null, out moving, out pending);
}
