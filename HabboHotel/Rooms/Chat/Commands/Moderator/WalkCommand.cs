using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :walk h/v username - forces the target player to patrol back
/// and forth along the given axis (same lane scan as the bot patrol, no
/// pause at the ends). Ends the moment the player clicks a tile of their
/// own; re-issuing swaps the axis.
/// </summary>
internal class WalkCommand : IChatCommand
{
    public string Key => "walk";

    public string PermissionRequired => "command_walk";

    public string Parameters => "%h/v% %username%";

    public string Description => "Force a player to walk back and forth horizontally or vertically.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length < 2)
        {
            session.SendWhisper("Usage: :walk h/v username");
            return;
        }
        var axis = parameters[0].ToLowerInvariant();
        if (axis != "h" && axis != "v")
        {
            session.SendWhisper("Axis must be h (horizontal) or v (vertical).");
            return;
        }
        var target = room.GetRoomUserManager().GetRoomUserByHabbo(parameters[1]);
        if (target == null || target.IsBot)
        {
            session.SendWhisper("That player is not in this room.");
            return;
        }
        target.ForcedWalkHorizontal = axis == "h";
        target.ForcedWalkDirection = 1;
        session.SendWhisper($"{target.GetUsername()} is now walking {(axis == "h" ? "horizontally" : "vertically")} - they can break out by walking somewhere themselves.");
    }
}
