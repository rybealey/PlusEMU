using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

/// <summary>
/// pixelrp: toggles whether one rank may work in this room's headquarters.
/// Staff only; the rank must belong to the room's assigned corporation.
/// </summary>
internal class RpSetHqRankEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var rankId = packet.ReadInt();
        var authorized = packet.ReadInt() == 1;
        var room = session.GetHabbo()?.CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        if (!room.CheckRights(session, true))
            return Task.CompletedTask;
        if (session.GetHabbo().Rank < 5)
            return Task.CompletedTask;
        if (room.CorporationId <= 0)
            return Task.CompletedTask;

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            // Rank must belong to this room's corp.
            var ok = connection.QuerySingleOrDefault<int?>(
                "SELECT `id` FROM `rp_corporation_ranks` WHERE `id` = @rankId AND `corporation_id` = @corpId LIMIT 1",
                new { rankId, corpId = room.CorporationId });
            if (ok == null)
                return Task.CompletedTask;
            if (authorized)
                connection.Execute("INSERT IGNORE INTO `rp_hq_room_ranks` (`room_id`, `rank_id`) VALUES (@roomId, @rankId)",
                    new { roomId = room.Id, rankId });
            else
                connection.Execute("DELETE FROM `rp_hq_room_ranks` WHERE `room_id` = @roomId AND `rank_id` = @rankId",
                    new { roomId = room.Id, rankId });
        }
        session.Send(CorporationUtility.BuildRoomCorp(room));
        return Task.CompletedTask;
    }
}
