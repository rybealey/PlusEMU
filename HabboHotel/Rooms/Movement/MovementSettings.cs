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
    /// server_settings key: <c>movement.v2.enabled</c>.
    ///
    /// CAUTION - SettingsManager.TryGetValue returns the STRING "0" for a
    /// MISSING key (Core/Settings/SettingsManager.cs:26). So "0" cannot be
    /// distinguished from "absent", and the ONLY safe encoding for a
    /// default-off flag is "enabled iff the value is exactly 1". Do not invert
    /// this into a "disabled" key - that is the V1 trap that made
    /// pathfinder.formation.window.ms unable to express "off".
    /// </summary>
    public const string EnabledKey = "movement.v2.enabled";

    /// <summary>Read live so the flag can be flipped without a restart.</summary>
    public static bool Enabled => PlusEnvironment.SettingsManager.TryGetValue(EnabledKey) == "1";

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
