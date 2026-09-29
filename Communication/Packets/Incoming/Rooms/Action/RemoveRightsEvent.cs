using Plus.Communication.Packets.Outgoing.Rooms.Permissions;
using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Action;

internal class RemoveRightsEvent : RoomPacketEvent
{
    private readonly IDatabase _database;

    public RemoveRightsEvent(IDatabase database)
    {
        _database = database;
    }

    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        // pixelrp: staff only - owners no longer take rights away
        if (!room.CanManageSettings(session))
            return Task.CompletedTask;
        var amount = packet.ReadInt();
        for (var i = 0; i < amount && i <= 100; i++)
        {
            var userId = packet.ReadInt();
            if (userId > 0 && room.UsersWithRights.Contains(userId))
            {
                var user = room.GetRoomUserManager().GetRoomUserByHabbo(userId);
                if (user != null && !user.IsBot)
                {
                    user.RemoveStatus("flatctrl 1");
                    user.UpdateNeeded = true;
                    user.GetClient().Send(new YouAreControllerComposer(0));
                }
                using (var dbClient = _database.GetQueryReactor())
                {
                    dbClient.SetQuery("DELETE FROM `room_rights` WHERE `user_id` = @uid AND `room_id` = @rid LIMIT 1");
                    dbClient.AddParameter("uid", userId);
                    dbClient.AddParameter("rid", room.Id);
                    dbClient.RunQuery();
                }
                if (room.UsersWithRights.Contains(userId))
                    room.UsersWithRights.Remove(userId);
                session.Send(new FlatControllerRemovedComposer(room, userId));
            }
        }
        return Task.CompletedTask;
    }
}