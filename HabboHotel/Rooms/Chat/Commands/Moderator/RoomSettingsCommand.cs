using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :roomsettings - open the room settings window for the room the
/// caller is standing in, without going through the navigator. Staff only.
/// Mirrors GetRoomSettingsEvent's sends (stock settings + zone + HQ config).
/// </summary>
internal class RoomSettingsCommand : IChatCommand
{
    public string Key => "roomsettings";
    public string PermissionRequired => "command_roomsettings";

    public string Parameters => "";

    public string Description => "Open room settings for the current room.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (room == null)
            return;
        session.Send(new RoomSettingsDataComposer(room));
        session.Send(new RpRoomZoneComposer(room.Id, room.IsSafeZone));
        session.Send(CorporationUtility.BuildRoomCorp(room));
    }
}
