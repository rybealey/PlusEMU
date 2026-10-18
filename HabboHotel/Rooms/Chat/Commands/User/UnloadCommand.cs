using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// :unload - unload the room you are in; it takes no room id. Staff only
/// (command_unload, Trial Moderator and up), and since it is, it no longer
/// also asks for room rights or room_unload_any (Developer+) - with them, a
/// moderator in someone else's room was silently refused (Twist, 2026-10-18).
/// </summary>
internal class UnloadCommand : IChatCommand
{
    private readonly IRoomManager _roomManager;
    public string Key => "unload";
    public string PermissionRequired => "command_unload";

    public string Parameters => "";

    public string Description => "Unload the room you are in.";

    public UnloadCommand(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }
    public void Execute(GameClient session, Room room, string[] parameters)
    {
        _roomManager.UnloadRoom(room.Id);
    }
}
