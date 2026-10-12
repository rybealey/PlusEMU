using System.Drawing;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :walk h/v/d1/d2 name - forces the target to patrol back and forth
/// along the given axis (same lane scan as the bot patrol, no pause at the
/// ends). A player or a bot (not a pet): a bot's name may hold spaces, so the
/// name is everything after the axis, and players are looked for first. A
/// player ends it by clicking a tile of their own; a bot never clicks, so
/// :walk stop name ends it for either. Re-issuing swaps the axis.
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

    public string Parameters => "%h/v/d1/d2/stop% %name%";

    public string Description => "Force a player or bot to walk back and forth along an axis, including diagonals.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length < 2)
        {
            session.SendWhisper("Usage: :walk h/v/d1/d2/stop name.");
            return;
        }

        if (room == null)
        {
            session.SendWhisper("You need to be in a room to do that.");
            return;
        }

        var name = string.Join(" ", parameters.Skip(1));
        var manager = room.GetRoomUserManager();
        var target = manager?.GetRoomUserByHabbo(name);
        if (target != null && target.IsBot)
            target = null;
        target ??= manager?.GetBotByName(name);
        if (target == null)
        {
            session.SendWhisper($"{name} is not in this room.");
            return;
        }
        var targetName = target.IsBot ? target.BotData.Name : target.GetUsername();

        var axis = parameters[0].ToLowerInvariant();
        if (axis == "stop")
        {
            if (target.ForcedWalkAxis == null)
            {
                session.SendWhisper($"{targetName} is not on a :walk lane.");
                return;
            }
            target.ForcedWalkAxis = null;
            session.SendWhisper($"{targetName} has stopped walking.");
            return;
        }

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
            session.SendWhisper("Axis must be h, v, d1, d2 or stop.");
            return;
        }

        target.ForcedWalkAxis = step;
        target.ForcedWalkDirection = 1;
        target.ForcedWalkRandom = false; // a lane patrol replaces any wander
        session.SendWhisper(target.IsBot
            ? $"{targetName} is now walking {label} - :walk stop {targetName} ends it."
            : $"{targetName} is now walking {label} - they can break out by walking somewhere themselves.");
    }
}
