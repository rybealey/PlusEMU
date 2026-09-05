namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: the locked constants, plus the single kill switch that
/// decides whether V2 owns movement at all.
///
/// While <see cref="Enabled"/> is false the V2 scheduler exists, runs and can be
/// measured, but never owns or moves a normal user - V1 keeps every walker.
/// The flip to true is the hard cutover and is NOT part of this pass.
/// </summary>
public static class MovementSettings
{
    /// <summary>
    /// V2 is ALWAYS ON. There is no runtime kill switch by design.
    ///
    /// It shipped behind `movement.v2.enabled` while the foundation was being
    /// wired up, but a flag that can be half-on is its own hazard: it makes
    /// "which system is actually moving this avatar?" a question you have to
    /// answer before you can debug anything, and it leaves V1 and V2 both
    /// reachable in the same build.
    ///
    /// ROLLING BACK now means reverting the emulator commit and deploying -
    /// a few minutes, and it puts the hotel in one unambiguous state rather
    /// than a mixed one.
    ///
    /// The legacy server_settings row is simply ignored; updates 69-72 are
    /// left in place as history and are harmless.
    /// </summary>
    public const bool Enabled = true;

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
