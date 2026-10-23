using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// :unload - unload the room you are in; it takes no room id. Staff only
/// (command_unload, Builder and up), and since it is, it no longer
/// also asks for room rights or room_unload_any (Developer+) - with them, a
/// moderator in someone else's room was silently refused (Twist, 2026-10-18).
/// </summary>
internal class UnloadCommand : IChatCommand
{
    private readonly IRoomManager _roomManager;
    public string Key => "unload";
    public string PermissionRequired => "command_unload";

    public string Parameters => "";

    public string Description => "Reload the room you are in - everyone in it comes straight back.";

    public UnloadCommand(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }
    public void Execute(GameClient session, Room room, string[] parameters)
    {
        // pixelrp: everyone in the room comes straight back into it, freshly
        // loaded (RoomManager.ReloadRoom). Unloaded and left there, they still
        // pointed at the room just disposed, and the typing indicator going off
        // as the command went out threw on it and dropped the connection: the
        // disconnected screen (Twist, 2026-10-24).
        _roomManager.ReloadRoom(room);
    }
}
