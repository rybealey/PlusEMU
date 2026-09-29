using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :roomsettings - open the Mod Tools' Room tool for the room the
/// caller is standing in. The Room tool has the room's settings built in
/// (Overview, Roleplay, Moderation, Rights), so it replaces the owner's Room
/// settings window this command used to open. The tool asks for the settings
/// itself, and the server answers only while the caller may change the room.
/// Staff only.
/// </summary>
internal class RoomSettingsCommand : IChatCommand
{
    public string Key => "roomsettings";
    public string PermissionRequired => "command_roomsettings";

    public string Parameters => "";

    public string Description => "Open the Room tool for the current room.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (room == null)
            return;
        session.Send(new InClientLinkComposer($"mod-tools/open-room-info/{room.Id}"));
    }
}
