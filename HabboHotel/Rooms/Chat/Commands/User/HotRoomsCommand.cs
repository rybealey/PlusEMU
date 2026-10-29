using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :hotrooms - open a window of the five busiest rooms (Twist,
/// 2026-10-29). Every player; the room ids are shown to staff only. It keeps
/// itself up to date while open (HotRooms).
/// </summary>
internal class HotRoomsCommand : IChatCommand
{
    public string Key => "hotrooms";

    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "See the busiest rooms.";

    public void Execute(GameClient session, Room room, string[] parameters) => HotRooms.Open(session);
}
