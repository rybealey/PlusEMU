using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

/// <summary>
/// pixelrp: toggles which outside emergency service (0 medical, 1 police,
/// 2 staff) may work in this room. Editable by staff only
/// (Room.CanManageSettings), like the HQ settings.
///
/// Also the room's two police tags, 3 arrest room and 4 jail room (JailState),
/// on the same packet rather than a new one: a staff switch on this room, and
/// the same staff gate. Those two are answered with RpRoomPoliceComposer, the
/// packet the Gameplay tab reads them from.
/// </summary>
internal class RpSetEmergencyEvent : IPacketEvent
{
    private readonly IRoomManager _roomManager;

    public RpSetEmergencyEvent(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roomId = packet.ReadUInt();
        var category = packet.ReadInt();
        var enabled = packet.ReadInt() == 1;
        if (!_roomManager.TryLoadRoom(roomId, out var room))
            return Task.CompletedTask;
        if (!room.CanManageSettings(session))
            return Task.CompletedTask;

        string column;
        switch (category)
        {
            case 0:
                column = "allow_medical";
                room.AllowMedical = enabled;
                break;
            case 1:
                column = "allow_police";
                room.AllowPolice = enabled;
                break;
            case 2:
                column = "allow_staff";
                room.AllowStaff = enabled;
                break;
            case 3:
                column = "rp_arrest_room";
                room.IsArrestRoom = enabled;
                break;
            case 4:
                column = "rp_jail_room";
                room.IsJailRoom = enabled;
                break;
            default:
                return Task.CompletedTask;
        }
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute($"UPDATE `rooms` SET `{column}` = @val WHERE `id` = @roomId LIMIT 1",
                new { val = enabled ? "1" : "0", roomId = room.Id });
        }
        session.Send(CorporationUtility.BuildRoomCorp(room));
        if (category >= 3)
        {
            // The jail moved, or grew: prisoners are held to the new set.
            JailState.ForgetJailRooms();
            session.Send(new RpRoomPoliceComposer(room.Id, room.IsArrestRoom, room.IsJailRoom));
        }
        return Task.CompletedTask;
    }
}
