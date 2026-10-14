using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.Database;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.Communication.Packets.Incoming.Catalog;

/// <summary>
/// pixelrp: the client asks for its favourite shop categories, once, the first
/// time the shop's category list is drawn. No payload.
/// </summary>
internal class RpCatalogFavouritesRequestEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpCatalogFavouritesRequestEvent(IDatabase database)
    {
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        session.Send(new RpCatalogFavouritesComposer(CatalogFavourites.Load(_database, AccountUtility.RootOf(habbo.Id))));
        return Task.CompletedTask;
    }
}
