using Plus.Communication.Attributes;
using System.Data;
using Plus.Communication.Packets.Outgoing.Marketplace;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Marketplace;

[StaffOnly]
internal class GetMarketplaceItemStatsEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public GetMarketplaceItemStatsEvent(IDatabase database)
    {
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var itemId = packet.ReadInt();
        var spriteId = packet.ReadUInt();
        DataRow row;
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT `avgprice` FROM `catalog_marketplace_data` WHERE `sprite` = @SpriteId LIMIT 1");
            dbClient.AddParameter("SpriteId", spriteId);
            row = dbClient.GetRow();
        }
        session.Send(new MarketplaceItemStatsComposer(itemId, spriteId, row != null ? Convert.ToInt32(row["avgprice"]) : 0));
        return Task.CompletedTask;
    }
}