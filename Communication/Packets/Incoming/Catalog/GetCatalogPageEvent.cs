using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Catalog.Utilities;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Catalog;

[StaffOnly]
public class GetCatalogPageEvent : IPacketEvent
{
    private readonly ICatalogManager _catalogManager;

    public GetCatalogPageEvent(ICatalogManager catalogManager)
    {
        _catalogManager = catalogManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var pageId = packet.ReadInt();
        var itemId = packet.ReadInt();
        var cataMode = packet.ReadString();
        if (!_catalogManager.TryGetPage(pageId, out var page))
            return Task.CompletedTask;
        if (!page.Enabled || !page.Visible || page.MinimumRank > session.GetHabbo().Rank || page.MinimumVip > session.GetHabbo().VipRank && session.GetHabbo().Rank == 1)
            return Task.CompletedTask;
        // Page offers use catalog_items.id, while legacy links use offer_id.
        itemId = CatalogLookup.ResolveSelection(page, itemId);
        session.Send(new CatalogPageComposer(page, cataMode, itemId));
        return Task.CompletedTask;
    }
}
