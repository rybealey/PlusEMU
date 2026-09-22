namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: the locked constants.
///
/// THERE IS NO KILL SWITCH, by design. V2 shipped behind
/// `movement.v2.enabled` while the foundation was being wired up, and an
/// `Enabled` const outlived it - always true, read by nothing, and deleted on
/// 2026-09-21 because a switch that looks live and is not is worse than none.
/// V1 was removed outright, so there is no second engine to fall back to:
/// rolling back means reverting the emulator commit and deploying. The legacy
/// server_settings row is ignored; SQL updates 69-72 are left as history.
/// </summary>
public static class MovementSettings
{
    /// <summary>
    /// THE interval. One validated tile per 500ms, and the default for every
    /// walker. It is still not a tunable: nothing reads a speed out of config
    /// and no command changes one (LOCK NOTE section 8).
    ///
    /// It is no longer a hard constant either, and the wording above used to
    /// say it was. Exactly ONE thing overrides it, per walker and per walk
    /// session - see <see cref="EscortIntervalMs"/>.
    /// </summary>
    public const int IntervalMs = 500;

    /// <summary>
    /// THE ONE EXCEPTION: an ambulance run, twice normal pace.
    ///
    /// A paramedic carrying an unconscious patient moves at one tile per
    /// 250ms. Nothing else in the hotel does, and nothing else may: this is
    /// not a speed setting, it is a property of one escort flavour, latched
    /// onto the walker that is running it (MovementState.IntervalMs).
    ///
    /// It was 125 - four times pace - for exactly one build, and was halved
    /// after seeing it on beta. Worth knowing if it is ever raised again: the
    /// avatar's walk animation runs at its own fixed rate whatever the tile
    /// interval, so the faster this gets the more the avatar slides rather
    /// than walks. That is a rendering limit, not something the server can
    /// tune away.
    ///
    /// IT MUST DIVIDE 500, and that is load-bearing rather than tidy. The
    /// room's movement phase is a 500ms grid, and a walk still joins it on a
    /// 500ms boundary, so a fast walker's edges land on 0, 250, 500 - every
    /// second one on the room grid, and its TimelineOrigin still satisfies the
    /// "one cycleStart % 500" property the phase diagnostic checks. An
    /// interval that did not divide 500 would drift off that grid and make the
    /// diagnostic read as a fault. 125 and 250 both hold; 200 and 300 do not.
    ///
    /// The client needs nothing for this: the interval has always travelled
    /// per edge on the wire, and the renderer interpolates and chains its
    /// lookahead from the edge's own value (500 is only its fallback).
    /// </summary>
    public const int EscortIntervalMs = 250;

    /// <summary>Future edges advertised alongside a real edge. LOCK NOTE: 3.</summary>
    public const int LookaheadMax = 3;

    /// <summary>Scheduler wake ceiling when idle; a Signal cuts this short.</summary>
    public const int MaxSleepMs = 15;

    /// <summary>Minimum sleep, so an empty hotel does not spin a core.</summary>
    public const int MinSleepMs = 1;

    /// <summary>A due time within this window of now is treated as due.</summary>
    public const int TickSlackMs = 2;

    /* ALIGNMENT IS UNCONDITIONAL, and there is no ceiling constant any more.
     *
     * A Standing->Moving click by a real user ALWAYS waits for the room's next
     * phase boundary. The distance to it is 0..IntervalMs-1, so it was always
     * within the old MaxStartDelayMs ceiling (which equalled IntervalMs) and
     * the "too far, start unaligned" branch could never be taken. The ceiling,
     * that branch and PhaseDecision.Skipped were all deleted on 2026-09-22 -
     * they were dead at this value, not merely unused.
     *
     * That guarantee is what makes "every concurrently moving real user shares
     * one cycleStart % IntervalMs" a property of the design rather than a
     * coincidence of timing.
     *
     * THE COST IS INPUT LATENCY: up to one interval before the avatar moves,
     * half of one on average, on every walk started while somebody else is
     * already walking.
     *
     * TO TRADE THAT BACK FOR RESPONSIVENESS, alignment has to become
     * opportunistic again: restore the ceiling, restore the comparison in
     * ResolveStartOrigin, and give the walker a phase decision for "did not
     * join". It is a real change, not a number - which is the honest position,
     * because the number alone has done nothing for as long as it equalled
     * IntervalMs.
     */

    /// <summary>
    /// Ceiling on rooms processed in one scheduler pass, so the loop always
    /// returns to the top, re-reads the clock and updates its heartbeat.
    /// A pass that could run forever cannot be observed as stuck.
    /// </summary>
    public const int MaxRoomsPerPass = 256;

    /// <summary>Deferred (non-latency-critical) emission cadence per room.</summary>
    public const int FlushIntervalMs = 100;

    /// <summary>Per-room per-pass drain fuse, so one room cannot monopolise the thread.</summary>
    public const int DrainBudgetUs = 200;

    /// <summary>Hard count fuse alongside <see cref="DrainBudgetUs"/>.</summary>
    public const int MaxDrainPerRoom = 512;

    /// <summary>Orphan-walker watchdog cadence (LOCK NOTE I-12).</summary>
    public const int WatchdogIntervalMs = 1000;

    /// <summary>Identical-target redirect debounce, so spam-clicking cannot spin A*.</summary>
    public const int RepathMinIntervalMs = 40;
}
