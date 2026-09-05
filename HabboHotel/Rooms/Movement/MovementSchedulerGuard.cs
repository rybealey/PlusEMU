using System.Diagnostics;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2 (A2): mechanical enforcement of invariant I-5.
///
/// The single hotel-wide movement scheduler thread may ONLY mutate MovementState,
/// commit due edges, plan/replan routes, stage frames and manage its heaps and
/// queues. It must never touch the database, a socket, a wired/furni callback,
/// disconnect or removal logic, arbitrary game code, or a blocking log sink.
///
/// That rule is the entire reason a single thread is safe for the whole hotel
/// (LOCK NOTE 2.3 / section 12). A rule enforced only by code review is a rule
/// that will be broken during implementation, so this asserts it in DEBUG at the
/// dangerous entry points instead.
///
/// The check is compiled out entirely in Release: <see cref="Assert"/> is marked
/// [Conditional("DEBUG")], so call sites cost nothing in production.
/// </summary>
public static class MovementSchedulerGuard
{
    private static int _schedulerThreadId = -1;

    /// <summary>Called once by the scheduler thread as it starts.</summary>
    public static void MarkCurrentThreadAsScheduler() =>
        Interlocked.Exchange(ref _schedulerThreadId, Environment.CurrentManagedThreadId);

    public static void ClearSchedulerThread() =>
        Interlocked.Exchange(ref _schedulerThreadId, -1);

    /// <summary>True when the calling thread is the movement scheduler.</summary>
    public static bool OnSchedulerThread =>
        Volatile.Read(ref _schedulerThreadId) == Environment.CurrentManagedThreadId;

    /// <summary>
    /// Assert that forbidden work is NOT running on the scheduler thread.
    /// <paramref name="what"/> names the operation so a failure is self-explaining.
    ///
    /// Placement guidance: the top of database helpers, socket sends, wired
    /// dispatch entry points and disconnect/removal paths.
    /// </summary>
    [Conditional("DEBUG")]
    public static void Assert(string what)
    {
        if (!OnSchedulerThread)
            return;

        var message =
            $"[MOVEMENT_V2] '{what}' ran on the movement scheduler thread. " +
            "Invariant I-5 forbids DB, socket, callback, disconnect and blocking " +
            "work there - hand it to Q1/Q2 or a background worker instead.";

        Debug.Fail(message);
        throw new InvalidOperationException(message);
    }
}
