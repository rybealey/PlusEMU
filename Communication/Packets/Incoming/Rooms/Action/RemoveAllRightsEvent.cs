using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Permissions;
using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Action;

internal class RemoveAllRightsEvent : RoomPacketEvent
{
    private readonly IRoomManager _roomManager;
    private readonly IDatabase _database;

    public RemoveAllRightsEvent(IRoomManager roomManager, IDatabase database)
    {
        _roomManager = roomManager;
        _database = database;
    }

    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var instance = room;
        // pixelrp: staff only - owners no longer clear rights
        if (!instance.CanManageSettings(session))
            return Task.CompletedTask;
        var hadRights = new List<int>(instance.UsersWithRights);
        foreach (var userId in hadRights)
        {
            using (var dbClient = _database.GetQueryReactor())
            {
                dbClient.SetQuery("DELETE FROM `room_rights` WHERE `user_id` = @uid AND `room_id` = @rid LIMIT 1");
                dbClient.AddParameter("uid", userId);
                dbClient.AddParameter("rid", instance.Id);
                dbClient.RunQuery();
            }
            session.Send(new FlatControllerRemovedComposer(instance, userId));
            session.Send(new RoomRightsListComposer(instance));
            instance.SendToViewer(session, new UserUpdateComposer(instance.GetRoomUserManager().GetUserList().ToList()));
        }
        if (instance.UsersWithRights.Count > 0)
            instance.UsersWithRights.Clear();
        // pixelrp: the badge as entry gives it (RoomUserManager.ApplyRoomRights),
        // worked out from the rights as they now are. Set here by hand under
        // its own key ("flatctrl 1"), it never matched entry's "flatctrl", so a
        // badge given one way was never taken off the other.
        foreach (var userId in hadRights)
        {
            var user = instance.GetRoomUserManager().GetRoomUserByHabbo(userId);
            if (user != null && !user.IsBot)
                instance.GetRoomUserManager().PushRoomRights(user.GetClient());
        }
        return Task.CompletedTask;
    }
}