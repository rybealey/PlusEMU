using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Catalog.Utilities;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Catalog;

/// Resolves a rendered furniture type against the current shop, independently
/// of FurnitureData.offerid (which is absent on imported furniture).
internal class RpCatalogLocateEvent : IPacketEvent
{
    private readonly ICatalogManager _catalog;

    public RpCatalogLocateEvent(ICatalogManager catalog) => _catalog = catalog;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var requestId = packet.ReadInt();
        var spriteId = packet.ReadInt();
        var wall = packet.ReadBool();
        var habbo = session.GetHabbo();
        // The catalog and purchase handlers are staff-only. A negative result
        // also lets the infostand hide Buy for furniture with no accessible shelf.
        var item = habbo != null
            ? CatalogLookup.FindFurniture(_catalog.Pages, spriteId, wall, habbo.IsStaff, habbo.Rank, habbo.VipRank)
            : null;

        session.Send(new RpCatalogLocateComposer(requestId, item?.PageId ?? -1, item?.Id ?? -1));
        return Task.CompletedTask;
    }
}
