using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

/// <summary>
/// pixelrp: assigns (or clears, corpId 0) this room as a corporation's
/// headquarters. Staff only. On assign, seeds all of the corp's ranks as
/// authorized; on clear/reassign, drops the room's rank rows first.
/// </summary>
internal class RpSetRoomCorpEvent : IPacketEvent
{
    private readonly IRoomManager _roomManager;

    public RpSetRoomCorpEvent(IRoomManager roomManager)
    {
        _roomManager = roomManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roomId = packet.ReadUInt();
        var corpId = packet.ReadInt();
        if (!_roomManager.TryLoadRoom(roomId, out var room))
            return Task.CompletedTask;
        if (!room.CheckRights(session, true))
            return Task.CompletedTask;
        if (session.GetHabbo().Rank < 5)
            return Task.CompletedTask;

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            // Validate the corp exists (or 0 to clear).
            if (corpId > 0)
            {
                var exists = connection.QuerySingleOrDefault<int?>(
                    "SELECT `id` FROM `rp_corporations` WHERE `id` = @corpId LIMIT 1", new { corpId });
                if (exists == null)
                    corpId = 0;
            }

            connection.Open();
            using var transaction = connection.BeginTransaction();

            connection.Execute("UPDATE `rooms` SET `corporation_id` = @corpId WHERE `id` = @roomId LIMIT 1",
                new { corpId, roomId = room.Id }, transaction);
            connection.Execute("DELETE FROM `rp_hq_room_ranks` WHERE `room_id` = @roomId", new { roomId = room.Id }, transaction);
            if (corpId > 0)
                connection.Execute(
                    "INSERT INTO `rp_hq_room_ranks` (`room_id`, `rank_id`) " +
                    "SELECT @roomId, `id` FROM `rp_corporation_ranks` WHERE `corporation_id` = @corpId",
                    new { roomId = room.Id, corpId }, transaction);

            transaction.Commit();
        }
        room.CorporationId = corpId;
        session.Send(CorporationUtility.BuildRoomCorp(room));
        return Task.CompletedTask;
    }
}
