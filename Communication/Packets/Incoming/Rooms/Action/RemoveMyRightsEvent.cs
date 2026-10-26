using Plus.Communication.Packets.Outgoing.Rooms.Permissions;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Action;

internal class RemoveMyRightsEvent : RoomPacketEvent
{
    private readonly IDatabase _database;

    public RemoveMyRightsEvent(IDatabase database)
    {
        _database = database;
    }

    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        if (!room.CheckRights(session, false))
            return Task.CompletedTask;
        if (room.UsersWithRights.Contains(session.GetHabbo().Id))
        {
            using (var dbClient = _database.GetQueryReactor())
            {
                dbClient.SetQuery("DELETE FROM `room_rights` WHERE `user_id` = @uid AND `room_id` = @rid LIMIT 1");
                dbClient.AddParameter("uid", session.GetHabbo().Id);
                dbClient.AddParameter("rid", room.Id);
                dbClient.RunQuery();
            }
            if (room.UsersWithRights.Contains(session.GetHabbo().Id))
                room.UsersWithRights.Remove(session.GetHabbo().Id);
            // pixelrp: the badge as entry gives it (RoomUserManager.ApplyRoomRights),
            // worked out from the rights as they now are. Set here by hand under
            // its own key ("flatctrl 1"), it never matched entry's "flatctrl", so a
            // badge given one way was never taken off the other.
            room.GetRoomUserManager().PushRoomRights(session);
        }
        return Task.CompletedTask;
    }
}