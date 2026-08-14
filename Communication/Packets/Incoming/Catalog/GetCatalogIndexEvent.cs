using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.BuildersClub;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Catalog;

[StaffOnly]
public class GetCatalogIndexEvent : IPacketEvent
{
    private readonly ICatalogManager _catalogManager;

    public GetCatalogIndexEvent(ICatalogManager catalogManager)
    {
        _catalogManager = catalogManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(new CatalogIndexComposer(session, _catalogManager.Pages));
        session.Send(new CatalogItemDiscountComposer());
        session.Send(new BcBorrowedItemsComposer());
        return Task.CompletedTask;
    }
}