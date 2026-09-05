using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Administrator;

/// <summary>
/// pixelrp Movement V2: READ-ONLY diagnostics. Changes nothing.
///
///   :movementstats
///
/// Movement V2 is always on and has no toggle; this exists purely because the
/// emulator log is not reachable from the dev machine, so a freeze otherwise
/// has to be debugged by guesswork.
///
/// The decisive field is roomFaults: non-zero means a room threw inside the
/// movement scheduler, which closes that room permanently and freezes every
/// walker in it. framesHandedOff tells you whether the scheduler is still
/// producing at all - if it stops climbing while an avatar is stuck, the
/// problem is upstream in the scheduler; if it keeps climbing, the problem is
/// downstream in apply/broadcast.
/// </summary>
internal class MovementStatsCommand : IChatCommand
{
    public string Key => "movementstats";
    public string PermissionRequired => "command_update";
    public string Parameters => "";
    public string Description => "Show live Movement V2 counters (read-only).";

    public void Execute(GameClient session, Room room, string[] parameters) =>
        session.SendWhisper(MovementRegistry.Snapshot());
}
