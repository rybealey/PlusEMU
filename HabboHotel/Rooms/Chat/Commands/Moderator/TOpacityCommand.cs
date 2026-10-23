using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :topacity - how see-through the floor tiles are in the room the
/// caller is standing in, 0 (gone) to 100 (solid); everyone in the room sees
/// it change, and it stays with the room (RoomFloorOpacity). The Room tool's
/// Floor opacity slider sets the same thing. With no number it says where the
/// room is now. Staff only: rank 5 and up (migration 306), as :roomsettings.
/// </summary>
internal class TOpacityCommand : IChatCommand
{
    public string Key => "topacity";
    public string PermissionRequired => "command_topacity";

    public string Parameters => "%0-100%";

    public string Description => "Set how see-through the floor tiles are in this room.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (room == null)
            return;
        if (parameters.Length == 0)
        {
            session.SendWhisper($"The floor here is at {room.FloorOpacity}%. :topacity 0-100 changes it - 0 is see-through, 100 is solid.");
            return;
        }
        // "50" or "50%"
        if (!int.TryParse(parameters[0].TrimEnd('%'), out var opacity) || opacity < 0 || opacity > RoomFloorOpacity.Solid)
        {
            session.SendWhisper("Floor opacity is a number from 0 (see-through) to 100 (solid).");
            return;
        }
        RoomFloorOpacity.Set(room, opacity);
        session.SendWhisper($"The floor here is now at {opacity}%.");
    }
}
