using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

internal class DeleteRoomEvent : IPacketEvent
{
    private readonly IGameClientManager _clientManager;
    private readonly IRoomManager _roomManager;
    private readonly IDatabase _database;

    public DeleteRoomEvent(IGameClientManager clientManager, IRoomManager roomManager, IDatabase database)
    {
        _clientManager = clientManager;
        _roomManager = roomManager;
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roomId = packet.ReadUInt();
        if (roomId == 0)
            return Task.CompletedTask;
        if (!_roomManager.TryGetRoom(roomId, out var room))
            return Task.CompletedTask;
        // pixelrp: staff only (rank 5+) - owning the room is no longer enough (Room tool)
        if (!room.CanManageSettings(session))
            return Task.CompletedTask;
        var itemsToRemove = new List<Item>();
        foreach (var item in room.GetRoomItemHandler().GetWallAndFloor.ToList())
        {
            if (item == null)
                continue;
            if (item.Definition.InteractionType == InteractionType.Moodlight)
            {
                using var dbClient = _database.GetQueryReactor();
                dbClient.SetQuery("DELETE FROM `room_items_moodlight` WHERE `item_id` = @itemId LIMIT 1");
                dbClient.AddParameter("itemId", item.Id);
                dbClient.RunQuery();
            }
            itemsToRemove.Add(item);
        }
        foreach (var item in itemsToRemove)
        {
            var targetClient = _clientManager.GetClientByUserId(item.UserId);
            if (targetClient != null && targetClient.GetHabbo() != null) //Again, do we have an active client?
            {
                room.GetRoomItemHandler().RemoveFurniture(targetClient, item.Id);
                targetClient.GetHabbo().Inventory.Furniture.AddItem(item.ToInventoryItem());
                targetClient.Send(new FurniListUpdateComposer());
            }
            else //No, query time.
            {
                room.GetRoomItemHandler().RemoveFurniture(null, item.Id);
                using var dbClient = _database.GetQueryReactor();
                dbClient.SetQuery("UPDATE `items` SET `room_id` = '0' WHERE `id` = @itemId LIMIT 1");
                dbClient.AddParameter("itemId", item.Id);
                dbClient.RunQuery();
            }
        }
        _roomManager.UnloadRoom(room.Id);
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.RunQuery($"DELETE FROM `user_roomvisits` WHERE `room_id` = '{roomId}'");
            dbClient.RunQuery($"DELETE FROM `rooms` WHERE `id` = '{roomId}' LIMIT 1");
            dbClient.RunQuery($"DELETE FROM `user_favorites` WHERE `room_id` = '{roomId}'");
            dbClient.RunQuery($"DELETE FROM `items` WHERE `room_id` = '{roomId}'");
            dbClient.RunQuery($"DELETE FROM `room_rights` WHERE `room_id` = '{roomId}'");
            dbClient.RunQuery($"UPDATE `users` SET `home_room` = '0' WHERE `home_room` = '{roomId}'");
        }
        _roomManager.UnloadRoom(room.Id);
        return Task.CompletedTask;
    }
}
