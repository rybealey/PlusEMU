using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

internal class RightsCommand : IChatCommand
{
    public string Key => "rights";
    public string PermissionRequired => "command_rights";

    public string Parameters => "%on/off%";

    public string Description => "Enable or disable your room rights in the room you are standing in.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var argument = parameters.Length > 0 ? parameters[0].ToLower() : "";
        if (argument != "on" && argument != "off")
        {
            session.SendWhisper($"Your room rights are currently {(session.GetHabbo().RoomRightsEnabled ? "ON" : "OFF")}. Use :rights on or :rights off to change them.");
            return;
        }
        session.GetHabbo().RoomRightsEnabled = argument == "on";
        session.GetHabbo().RoomRightsRoomId = room.Id;
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user != null)
            room.GetRoomUserManager().RefreshRights(session, user);
        session.SendWhisper(session.GetHabbo().RoomRightsEnabled
            ? "Room rights enabled - they reset when you leave the room or log out."
            : "Room rights disabled.");
    }
}
