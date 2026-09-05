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
/// </summary>
internal class MovementStatsCommand : IChatCommand
{
    public string Key => "movementstats";
    public string PermissionRequired => "command_update";
    public string Parameters => "";
    public string Description => "Show live Movement V2 threads, counters and walkers (read-only).";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        session.SendWhisper(MovementRegistry.Health());
        session.SendWhisper(MovementRegistry.LastFault());
        session.SendWhisper(MovementRegistry.Snapshot());

        if (room == null)
            return;
        foreach (var line in MovementRegistry.DescribeRoom(room.RoomId))
            session.SendWhisper(line);
    }
}
