using System.Drawing;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :walk h/v/d1/d2 username - forces the target player to patrol back
/// and forth along the given axis (same lane scan as the bot patrol, no pause at
/// the ends). Ends the moment the player clicks a tile of their own; re-issuing
/// swaps the axis.
///
/// The two diagonal lanes are the same scan with a different step vector, and
/// the pathfinder already walks diagonals (Neighbours8, with CornerPolicy.Off
/// imposing no corner restriction), so a diagonal lane needs nothing special of
/// the movement engine. With ForcedWalkDirection negating the axis, the four
/// options cover all eight headings.
/// </summary>
internal class WalkCommand : IChatCommand
{
    public string Key => "walk";

    public string PermissionRequired => "command_walk";

    public string Parameters => "%h/v/d1/d2% %username%";

    public string Description => "Force a player to walk back and forth along an axis, including diagonals.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length < 2)
        {
            session.SendWhisper("Usage: :walk h/v/d1/d2 username.");
            return;
        }

        if (room == null)
        {
            session.SendWhisper("You need to be in a room to do that.");
            return;
        }

        var axis = parameters[0].ToLowerInvariant();
        var (step, label) = axis switch
        {
            "h" => (new Point(1, 0), "horizontally"),
            "v" => (new Point(0, 1), "vertically"),
            "d" or "d1" => (new Point(1, 1), "diagonally"),
            "d2" => (new Point(1, -1), "diagonally the other way"),
            _ => (Point.Empty, null as string)
        };

        if (label == null)
        {
            session.SendWhisper("Axis must be h, v, d1 or d2.");
            return;
        }

        var target = room.GetRoomUserManager()?.GetRoomUserByHabbo(parameters[1]);
        if (target == null || target.IsBot)
        {
            session.SendWhisper("That player is not in this room.");
            return;
        }

        target.ForcedWalkAxis = step;
        target.ForcedWalkDirection = 1;
        target.ForcedWalkRandom = false; // a lane patrol replaces any wander
        session.SendWhisper(
            $"{target.GetUsername()} is now walking {label} - they can break out by walking somewhere themselves.");
    }
}
