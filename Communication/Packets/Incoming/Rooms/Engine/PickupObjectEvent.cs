using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

internal class PickupObjectEvent : IPacketEvent
{
    private readonly IGameClientManager _clientManager;
    private readonly IQuestManager _questManager;
    private readonly IDatabase _database;

    public PickupObjectEvent(IGameClientManager clientManager, IQuestManager questManager, IDatabase database)
    {
        _clientManager = clientManager;
        _questManager = questManager;
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().InRoom)
            return;
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return;
        packet.ReadInt(); //unknown
        var itemId = packet.ReadUInt();
        var item = room.GetRoomItemHandler().GetItem(itemId);
        if (item == null)
            return;
        if (item.Definition.InteractionType == InteractionType.Postit)
            return;
        // pixelrp: `room_item_take` is the one destructive override that does
        // not go through CheckRights, so the on-duty gate has to be repeated
        // here. This is the EJECTION path - it moves somebody else's furni out
        // of the room, and the second test below hands it to whoever pressed
        // the button rather than to its owner. Off duty, neither answers.
        var canTakeAnyones = session.GetHabbo().Permissions.HasRight("room_item_take") &&
                             HabboHotel.Corporations.ShiftManager.IsStaffOnDuty(session.GetHabbo().Id);
        // pixelrp: an apartment's arrow is its only way out (RpApartments) -
        // its owner can move it but not pick it up; on-duty staff still can.
        if (item.Definition.InteractionType == InteractionType.ApartmentExit && !canTakeAnyones)
            return;
        // pixelrp: in a PixelRP room, furni a Builder or above picks up becomes
        // theirs - into their own inventory, never sent to PixelRP's, where
        // nobody could reach it (PixelRpRooms). Only used once itemRights
        // below has said they may pick it up at all.
        var builderTakesIt = PixelRpRooms.BuilderTakesFurni(room, session);

        var itemRights = false;
        if (item.UserId == session.GetHabbo().Id || room.CheckRights(session, false))
            itemRights = true;
        else if (room.Group != null && room.CheckRights(session, false, true)) //Room has a group, this user has group rights.
            itemRights = true;
        else if (canTakeAnyones)
            itemRights = true;
        if (itemRights)
        {
            using var connection = _database.Connection();
            if (item.Definition.InteractionType == InteractionType.Tent || item.Definition.InteractionType == InteractionType.TentSmall)
                room.RemoveTent(item.Id);
            if (item.Definition.InteractionType == InteractionType.Moodlight)
            {
                await connection.ExecuteAsync("DELETE FROM `room_items_moodlight` WHERE `item_id` = @id LIMIT 1", new { id = item.Id });
            }
            else if (item.Definition.InteractionType == InteractionType.Toner)
            {
                await connection.ExecuteAsync("DELETE FROM `room_items_toner` WHERE `id` = @id LIMIT 1", new { id = item.Id });
            }
            if (item.UserId == session.GetHabbo().Id || canTakeAnyones || builderTakesIt)
            {
                if (builderTakesIt)
                    PixelRpRooms.SetItemOwner(item, session.GetHabbo().Id, session.GetHabbo().Username);
                room.GetRoomItemHandler().RemoveFurniture(session, item.Id);
                session.GetHabbo().Inventory.Furniture.AddItem(item.ToInventoryItem());
                session.Send(new FurniListUpdateComposer());
            }
            else //Item is being ejected.
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
                }
            }

            if (builderTakesIt)
                await connection.ExecuteAsync("UPDATE `items` SET `room_id` = '0', `user_id` = @owner WHERE `id` = @id LIMIT 1",
                    new { id = item.Id, owner = session.GetHabbo().Id });
            else
                await connection.ExecuteAsync("UPDATE `items` SET `room_id` = '0' WHERE `id` = @id LIMIT 1", new { id = item.Id });

            _questManager.ProgressUserQuest(session, QuestType.FurniPick);
        }
    }
}
