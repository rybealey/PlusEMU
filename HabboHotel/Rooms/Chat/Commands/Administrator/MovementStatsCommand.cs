using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Administrator;

/// <summary>
/// pixelrp Movement V2: READ-ONLY diagnostics. Changes nothing.
///
///   :movementstats          threads, counters, and this room's live walkers
///
/// Movement V2 is always on and has no toggle; this exists purely because the
/// emulator log is not reachable from the dev machine, so a freeze otherwise has
/// to be debugged by guesswork.
///
/// HOW TO READ IT DURING A FREEZE, in order:
///
///  1. sched alive=False        the hotel's movement thread is dead. Nothing
///                              moves anywhere and no click can recover it.
///  2. sched loopAge huge       the thread is alive but wedged inside one beat.
///  3. schedulerFaults > 0      a beat threw; :movementstats prints the last one.
///                              The beat itself survives now, so this is a
///                              symptom to chase, not the freeze.
///  4. room CLOSED              this room threw inside the scheduler and was
///                              retired. Every avatar in it is frozen for good.
///  5. q1Age huge / q1Depth up  frames are being sealed but not sent: the
///                              outbound worker is stuck, almost always waiting
///                              on RoomUserManager's _cycleLock.
///  6. unit mode=Moving with
///     dueIn far negative       the scheduler has stopped draining this walker.
///  7. everything healthy and
///     frames still climbing    the server is fine; the fault is client-side.
///
///   :movementstats reset    zeroes the [MV2/TIMING] histograms only, so a
///                           test reads as "since I started". The other
///                           counters stay cumulative since boot.
///
/// HOW TO READ [MV2/TIMING] (all sub-500ms; n = samples, then buckets):
///   stepLate         how late each step really began, lock wait included.
///                    Anything past <=10ms is a beat the client may draw
///                    from a stale preview.
///   schedLockWait    the scheduler waiting for a room (usually a click
///                    holding it: see clickLockHold).
///   clickLockWait/Hold   a click's wait for the room, then how long it
///                    holds it - route search included.
///   searchTime/Tiles one route search: time and tiles looked at.
///   frameToSend      beat start to the sender picking the frame up; this is
///                    how old the packet's server time is when it is built.
///   senderLockWait/Hold  the one sender thread's wait for a room's lock,
///                    then applying + sending. A long wait stalls EVERY room.
///   roomTickLockHold the 500ms room tick holding that same lock.
/// </summary>
internal class MovementStatsCommand : IChatCommand
{
    public string Key => "movementstats";
    public string PermissionRequired => "command_update";
    public string Parameters => "%reset%";
    public string Description => "Show live Movement V2 threads, counters, timings and walkers (read-only; reset zeroes the timings).";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length > 0 && parameters[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            MovementTiming.Reset();
            session.SendWhisper("Movement timings reset. Counting from now.");
            return;
        }

        session.SendWhisper(MovementRegistry.Health());
        session.SendWhisper(MovementRegistry.LastFault());
        session.SendWhisper(MovementRegistry.Snapshot());
        foreach (var line in MovementTiming.Describe())
            session.SendWhisper(line);

        if (room == null)
            return;
        foreach (var line in MovementRegistry.DescribeRoom(room.RoomId))
            session.SendWhisper(line);
    }
}
