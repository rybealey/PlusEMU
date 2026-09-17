using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Database;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// pixelrp: rename a furni definition from the catalog, without owning one.
///
/// The Function tool can already do this, but only to a piece PLACED in the
/// room you are standing in - RpFurniFunctionEvent resolves the definition
/// through the room on purpose, so a client cannot name an arbitrary id it
/// never had in front of it. Fixing a name in the shop meant buying the furni
/// first, which is a silly tax on tidying up a catalogue page.
///
/// This is the same edit with a different proof of "I am looking at it": the
/// OFFER has to be on a catalog page. That is exactly as hard to forge as the
/// room check and costs nobody a purchase.
///
/// Keyed by offer id, not definition id, because the client does not have the
/// definition id to send: CatalogPageComposer writes Definition.SpriteId, and a
/// sprite id is shared between colour variants - renaming by one would rename
/// whichever variant was found first. The offer id is what the client already
/// holds (Offer.offerId, written as item.Id) and it names exactly one row.
///
/// Deliberately narrow. The Function tool changes fourteen fields, several of
/// which (walkability, seating, interaction) need a map regeneration and a
/// rebroadcast to be safe. This changes ONE - the name - so none of that
/// applies, and the packet cannot be used to reach the rest.
/// </summary>
internal class RpCatalogRenameFurniEvent : IPacketEvent
{
    private const int MaximumNameLength = 56; // `furniture`.`public_name` is varchar(56)

    private readonly IDatabase _database;
    private readonly IGameClientManager _clientManager;
    private readonly ICatalogManager _catalogManager;
    private readonly IItemDataManager _itemDataManager;

    public RpCatalogRenameFurniEvent(IDatabase database, IGameClientManager clientManager,
        ICatalogManager catalogManager, IItemDataManager itemDataManager)
    {
        _database = database;
        _clientManager = clientManager;
        _catalogManager = catalogManager;
        _itemDataManager = itemDataManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var offerId = packet.ReadInt();
        var publicName = (packet.ReadString() ?? string.Empty).Trim();

        var habbo = session.GetHabbo();
        if (habbo == null || !habbo.Permissions.HasCommand("rp_furni_function"))
            return Task.CompletedTask;

        // Resolved through the catalog rather than trusted from the packet: an
        // offer that is not on any page is not something the sender was looking
        // at.
        ItemDefinition definition = null;
        foreach (var page in _catalogManager.Pages)
        {
            if (page?.Items == null)
                continue;
            foreach (var catalogItem in page.Items.Values)
            {
                if (catalogItem == null || catalogItem.Id != offerId || catalogItem.Definition == null)
                    continue;
                definition = catalogItem.Definition;
                break;
            }
            if (definition != null)
                break;
        }
        if (definition == null)
            return Task.CompletedTask;

        var definitionId = (int)definition.Id;

        // Wallpaper, floor and landscape carry an ID inside catalog_name rather
        // than a display name - the catalog composers pull it out with
        // Split('_')[2] - so renaming one corrupts the key and crashes the page
        // it sits on. RpFurniFunctionEvent refuses these the same way.
        if (IsStructuredCatalogName(definition.InteractionType))
            return Task.CompletedTask;

        // A blank name would leave the furni nameless everywhere it is listed.
        if (string.IsNullOrEmpty(publicName))
            return Task.CompletedTask;
        if (publicName.Length > MaximumNameLength)
            publicName = publicName.Substring(0, MaximumNameLength);

        var oldName = definition.PublicName ?? string.Empty;
        if (oldName == publicName)
            return Task.CompletedTask;

        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `furniture` SET `public_name` = @publicName WHERE `id` = @definitionId");
            dbClient.AddParameter("publicName", publicName);
            dbClient.AddParameter("definitionId", definitionId);
            dbClient.RunQuery();

            // The shop lists from catalog_items, not from the furniture row, so
            // a rename that stopped at `furniture` would leave the page still
            // selling the old name.
            dbClient.SetQuery("UPDATE `catalog_items` SET `catalog_name` = @publicName WHERE `item_id` = @itemIdText");
            dbClient.AddParameter("publicName", publicName);
            dbClient.AddParameter("itemIdText", definitionId.ToString());
            dbClient.RunQuery();

            // Same log as the Function tool writes, so a rename is auditable
            // the same way wherever it was made from.
            dbClient.SetQuery("INSERT INTO `rp_furni_function_log` (`definition_id`, `item_name`, `user_id`, " +
                              "`username`, `field`, `old_value`, `new_value`) VALUES (@definitionId, @itemName, " +
                              "@userId, @username, 'publicName', @oldValue, @newValue)");
            dbClient.AddParameter("definitionId", definitionId);
            dbClient.AddParameter("itemName", definition.ItemName ?? string.Empty);
            dbClient.AddParameter("userId", habbo.Id);
            dbClient.AddParameter("username", habbo.Username ?? string.Empty);
            dbClient.AddParameter("oldValue", oldName);
            dbClient.AddParameter("newValue", publicName);
            dbClient.RunQuery();
        }

        // In place, not a reload: every placed Item holds a reference to this
        // same object, so mutating it renames every copy at once.
        definition.PublicName = publicName;

        // The catalog is served from memory, so the pages hold their own copy of
        // the name and would go on showing the old one until a restart.
        foreach (var page in _catalogManager.Pages)
        {
            if (page?.Items == null)
                continue;
            foreach (var catalogItem in page.Items.Values)
            {
                if (catalogItem?.Definition?.Id == definition.Id)
                    catalogItem.CatalogName = publicName;
            }
        }

        // Remembered so a room can re-send this record to anyone entering it:
        // the client reads names out of gamedata on disk, which this does not
        // touch.
        _itemDataManager.EditedDefinitions.Add(definition.Id);

        _clientManager.SendPacket(new RpFurniFunctionComposer(definition));
        return Task.CompletedTask;
    }

    private static bool IsStructuredCatalogName(InteractionType type) =>
        type is InteractionType.Wallpaper or InteractionType.Floor or InteractionType.Landscape;
}
