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
    /// THE interval. One validated tile per 500ms, for every walker, always.
    /// There is no fast/superfast walking in V2 and no per-session interval:
    /// this is a constant, not a tunable (LOCK NOTE section 8).
    /// </summary>
    public const int IntervalMs = 500;

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
