using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

internal class SetMannequinNameEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public SetMannequinNameEvent(IDatabase database)
    {
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var room = session.GetHabbo().CurrentRoom;
        if (room == null || !room.CheckRights(session, true))
            return Task.CompletedTask;
        var itemId = packet.ReadUInt();
        var name = packet.ReadString();
        var item = session.GetHabbo().CurrentRoom.GetRoomItemHandler().GetItem(itemId);
        if (item == null)
            return Task.CompletedTask;
        var data = ItemBehaviourUtility.MannequinData(item);
        data.Set("OUTFIT_NAME", name);
        using (var dbClient = _database.GetQueryReactor())
        {
            // The serialized MAP, not LegacyDataString - that reads empty for
            // anything but a LegacyDataFormat and would have stored nothing.
            dbClient.SetQuery("UPDATE `items` SET `extra_data` = @Ed WHERE `id` = @itemId LIMIT 1");
            dbClient.AddParameter("itemId", item.Id);
            dbClient.AddParameter("Ed", item.ExtraData.Serialize());
            dbClient.RunQuery();
        }
        item.UpdateState(true, true);
        return Task.CompletedTask;
    }
}