using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Administrator;

/// <summary>
/// pixelrp Movement V2: paired timing trace. READ-ONLY - it changes no movement
/// behaviour, it only reports what the server already decided.
///
///   :movementtrace &lt;userA&gt; &lt;userB&gt;    trace two avatars in this room
///   :movementtrace off                    stop
///
/// Whispers, not logs, because the emulator log is not reachable from the dev
/// machine. Throttled to one line per avatar per 250ms, so a 500ms edge samples
/// at most twice and the chat stays readable.
///
/// READ gridPhase FIRST. It is cycleStart % 500 - the walker's phase against the
/// 500ms grid - and it is constant for a whole walk session. vsOther is the
/// difference between the two traced avatars.
///
///   vsOther != 0    the SERVER timelines are out of phase. The client is
///                   rendering two correctly-different timings, and no client
///                   change can align them.
///   vsOther == 0    the server agrees; compare the client's phase values next
///                   (pixelrpMovementDebug in the browser console).
/// </summary>
internal class MovementTraceCommand : IChatCommand
{
    public string Key => "movementtrace";
    public string PermissionRequired => "command_update";
    public string Parameters => "%userA% %userB%";
    public string Description => "Trace two avatars' Movement V2 edge timing (read-only).";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length >= 1 && parameters[0].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            MovementTrace.Stop();
            session.SendWhisper("Movement trace off.");
            return;
        }

        if (room == null)
        {
            session.SendWhisper("You need to be in a room to trace movement.");
            return;
        }

        if (parameters.Length < 2)
        {
            session.SendWhisper("Usage: :movementtrace <userA> <userB>, or :movementtrace off.");
            return;
        }

        var manager = room.GetRoomUserManager();
        var userA = manager?.GetRoomUserByHabbo(parameters[0]);
        var userB = manager?.GetRoomUserByHabbo(parameters[1]);

        if (userA == null)
        {
            session.SendWhisper($"{parameters[0]} is not in this room.");
            return;
        }

        if (userB == null)
        {
            session.SendWhisper($"{parameters[1]} is not in this room.");
            return;
        }

        MovementTrace.Start(session, userA.VirtualId, userB.VirtualId);
        session.SendWhisper(
            $"Movement trace on. A={userA.GetUsername()} (unit {userA.VirtualId}), " +
            $"B={userB.GetUsername()} (unit {userB.VirtualId}). Walk them both, then read gridPhase and vsOther.");
    }
}
