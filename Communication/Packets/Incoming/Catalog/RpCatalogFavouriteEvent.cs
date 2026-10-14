using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.Database;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Catalog.Utilities;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.Communication.Packets.Incoming.Catalog;

/// <summary>
/// pixelrp: star or unstar a shop category - the page id, then whether it is
/// now a favourite. The whole list goes back either way, so a refused star
/// (a page this player cannot open, a full list) puts the client right.
///
/// Only a page the player could be sent to can be starred: the same test the
/// Buy link uses (CatalogLookup.IsShoppable), and never a heading, a divider,
/// a mirror or a Favourites page itself. Taking a star off is always allowed,
/// so a category that has since been hidden can still be cleared.
/// </summary>
internal class RpCatalogFavouriteEvent : IPacketEvent
{
    private static readonly HashSet<string> NotFavourable = new() { "heading", "divider", "favourites", CatalogLookup.MirrorLink };

    private readonly IDatabase _database;
    private readonly ICatalogManager _catalogManager;

    public RpCatalogFavouriteEvent(IDatabase database, ICatalogManager catalogManager)
    {
        _database = database;
        _catalogManager = catalogManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var pageId = packet.ReadInt();
        var favourite = packet.ReadBool();

        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var accountId = AccountUtility.RootOf(habbo.Id);
        var current = CatalogFavourites.Load(_database, accountId);

        if (!favourite)
        {
            if (current.Remove(pageId))
                CatalogFavourites.Set(_database, accountId, pageId, false);
        }
        else if (!current.Contains(pageId) && current.Count < CatalogFavourites.Maximum &&
                 _catalogManager.TryGetPage(pageId, out var page) && page != null &&
                 !NotFavourable.Contains(page.Link ?? string.Empty) &&
                 CatalogLookup.IsShoppable(CatalogLookup.Index(_catalogManager.Pages), page, habbo.Rank, habbo.VipRank))
        {
            CatalogFavourites.Set(_database, accountId, pageId, true);
            current.Add(pageId);
        }

        session.Send(new RpCatalogFavouritesComposer(current));
        return Task.CompletedTask;
    }
}
