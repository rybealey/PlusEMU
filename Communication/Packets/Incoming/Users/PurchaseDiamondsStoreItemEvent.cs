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
        var slot = habbo.AddRpItem(item.ItemKey);
        if (slot == -1)
        {
            session.Send(new DiamondsStorePurchaseResultComposer(2, itemKey));
            return Task.CompletedTask;
        }
        habbo.Diamonds -= item.EffectivePrice;
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
