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
    /// THE ONE EXCEPTION: an ambulance run, four times normal pace.
    ///
    /// A paramedic carrying an unconscious patient moves at one tile per
    /// 125ms. Nothing else in the hotel does, and nothing else may: this is
    /// not a speed setting, it is a property of one escort flavour, latched
    /// onto the walker that is running it (MovementState.IntervalMs).
    ///
    /// 125 IS A DIVISOR OF 500, and that is load-bearing rather than tidy.
    /// The room's movement phase is a 500ms grid, and a walk still joins it on
    /// a 500ms boundary, so a fast walker's edges land on 0, 125, 250, 375,
    /// 500 - every fourth one on the room grid, and its TimelineOrigin still
    /// satisfies the "one cycleStart % 500" property the phase diagnostic
    /// checks. An interval that did not divide 500 would drift off that grid
    /// and make the diagnostic read as a fault.
    ///
    /// The client needs nothing for this: the interval has always travelled
    /// per edge on the wire, and the renderer interpolates and chains its
    /// lookahead from the edge's own value (500 is only its fallback).
    /// </summary>
    public const int EscortIntervalMs = 125;

    /// <summary>Future edges advertised alongside a real edge. LOCK NOTE: 3.</summary>
    public const int LookaheadMax = 3;

    /// <summary>Scheduler wake ceiling when idle; a Signal cuts this short.</summary>
    public const int MaxSleepMs = 15;

    /// <summary>Minimum sleep, so an empty hotel does not spin a core.</summary>
    public const int MinSleepMs = 1;

    /// <summary>A due time within this window of now is treated as due.</summary>
    public const int TickSlackMs = 2;

    /// <summary>
    /// Ceiling on how long a Standing-&gt;Moving click may be held back to join the
    /// room's movement phase.
    ///
    /// AT <see cref="IntervalMs"/> ALIGNMENT IS GUARANTEED: the distance to the
    /// next boundary is always 0..499, so it can never exceed the ceiling and a
    /// real user's walk always joins. That is what makes "every concurrently
    /// moving real user shares one cycleStart % 500" a property of the design
    /// rather than a coincidence of timing.
    ///
    /// THE COST IS INPUT LATENCY: up to 499ms before the avatar moves, ~250ms on
    /// average, on every walk started while somebody else is already walking.
    /// Lowering this makes alignment opportunistic again - walks whose boundary
    /// is further away start immediately and simply do not join, which trades
    /// perfect alignment for responsiveness. Nothing else needs to change to
    /// make that trade; PhaseDecision.Skipped already covers it.
    /// </summary>
    public const int MaxStartDelayMs = IntervalMs;

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
