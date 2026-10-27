using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.DiamondsStore;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

// pixelrp: buy a Store-tab item with diamonds. Delivery is into the RP
// backpack; failures never charge.
internal class PurchaseDiamondsStoreItemEvent : IPacketEvent
{
    private readonly IDiamondsStoreManager _storeManager;

    public PurchaseDiamondsStoreItemEvent(IDiamondsStoreManager storeManager) => _storeManager = storeManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var itemKey = packet.ReadString();
        var habbo = session.GetHabbo();
        if (habbo == null || !_storeManager.TryGetItem(itemKey, out var item))
            return Task.CompletedTask;
        if (habbo.Diamonds < item.EffectivePrice)
        {
            session.Send(new DiamondsStorePurchaseResultComposer(1, itemKey));
            return Task.CompletedTask;
        }
        // pixelrp: charge first, then deliver, and give it back if delivery
        // fails - so a backpack that turns out to be full (or a crash between
        // the two) can never hand over the item without taking the diamonds,
        // nor take them without handing it over.
        habbo.Diamonds -= item.EffectivePrice;
        var slot = habbo.AddRpItem(item.ItemKey);
        // Every failure, not only a full backpack: one it may hold only one of
        // (Habbo.RpAlreadyHeld) and a write the database refused (RpAddFailed)
        // took the diamonds and delivered nothing.
        if (slot < 0)
        {
            habbo.Diamonds += item.EffectivePrice;
            session.Send(new DiamondsStorePurchaseResultComposer(slot == -1 ? 2 : 3, itemKey));
            return Task.CompletedTask;
        }
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `users` SET `vip_points` = @diamonds WHERE `id` = @id LIMIT 1");
            dbClient.AddParameter("diamonds", habbo.Diamonds);
            dbClient.AddParameter("id", habbo.Id);
            dbClient.RunQuery();
            dbClient.SetQuery("INSERT INTO `diamonds_store_purchases` (`user_id`, `item_key`, `diamonds_paid`) VALUES (@id, @itemKey, @paid)");
            dbClient.AddParameter("id", habbo.Id);
            dbClient.AddParameter("itemKey", item.ItemKey);
            dbClient.AddParameter("paid", item.EffectivePrice);
            dbClient.RunQuery();
        }
        session.Send(new HabboActivityPointNotificationComposer(habbo.Diamonds, 0, 5));
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        session.Send(new DiamondsStorePurchaseResultComposer(0, itemKey));
        return Task.CompletedTask;
    }
}
