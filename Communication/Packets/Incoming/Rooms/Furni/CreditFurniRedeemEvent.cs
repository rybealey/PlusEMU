using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Core.Settings;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

internal class CreditFurniRedeemEvent : RoomPacketEvent
{
    private readonly ISettingsManager _settingsManager;
    private readonly IDatabase _database;

    public CreditFurniRedeemEvent(ISettingsManager settingsManager, IDatabase database)
    {
        _settingsManager = settingsManager;
        _database = database;
    }

    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        if (!room.CheckRights(session, true))
            return Task.CompletedTask;
        if (KnockedOut.Refuse(session))
            return Task.CompletedTask;
        if (_settingsManager.TryGetValue("room.item.exchangeables.enabled") != "1")
        {
            session.SendNotification("The hotel managers have temporarilly disabled exchanging!");
            return Task.CompletedTask;
        }
        var exchange = room.GetRoomItemHandler().GetItem(packet.ReadUInt());
        if (exchange == null)
            return Task.CompletedTask;
        if (exchange.Definition.InteractionType != InteractionType.Exchange)
            return Task.CompletedTask;
        // pixelrp: only your own credit furni. Owning the room was the only
        // test, and since Apartments every player owns one: a gold bar a
        // friend with rights put down there was the owner's to cash in. The
        // row goes first and it pays only once it is gone, so it pays once.
        if (exchange.UserId != session.GetHabbo().Id)
            return Task.CompletedTask;
        // Nor out of an apartment on the real estate board (RpApartments.IsListed).
        if (RpApartments.IsListed(room.RoomId))
        {
            session.SendWhisper(RpApartments.ListedFurniMessage);
            return Task.CompletedTask;
        }
        int removed;
        using (var connection = _database.Connection())
            removed = connection.Execute("DELETE FROM `items` WHERE `id` = @id LIMIT 1", new { id = exchange.Id });
        if (removed != 1)
            return Task.CompletedTask;
        var value = exchange.Definition.BehaviourData;
        if (value > 0)
        {
            session.GetHabbo().ChangeCredits(value);
            Plus.HabboHotel.Users.Banking.CoinLedger.Record(session.GetHabbo(), value, "Redeemed credit furni");
            session.Send(new CreditBalanceComposer(session.GetHabbo().Credits));
        }
        session.Send(new FurniListUpdateComposer());
        room.GetRoomItemHandler().RemoveFurniture(null, exchange.Id);
        session.GetHabbo().Inventory.Furniture.RemoveItem(exchange.Id);
        session.Send(new FurniListRemoveComposer(exchange.Id));
        return Task.CompletedTask;
    }
}