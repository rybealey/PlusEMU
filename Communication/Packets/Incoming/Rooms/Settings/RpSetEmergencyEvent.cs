using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

/// <summary>
/// pixelrp: toggles which outside emergency service (0 medical, 1 police,
/// 2 staff) may work in this room. Editable by the room owner or staff
/// (CheckRights), unlike the staff-only HQ settings.
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
        if (!room.CheckRights(session, true))
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
            default:
                return Task.CompletedTask;
        }
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute($"UPDATE `rooms` SET `{column}` = @val WHERE `id` = @roomId LIMIT 1",
                new { val = enabled ? "1" : "0", roomId = room.Id });
        }
        session.Send(CorporationUtility.BuildRoomCorp(room));
        return Task.CompletedTask;
    }
}
