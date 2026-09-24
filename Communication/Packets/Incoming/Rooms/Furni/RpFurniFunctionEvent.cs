using System.Globalization;
using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Database;
using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// pixelrp: applies a behaviour change to a furni DEFINITION, hotel-wide.
///
/// Three things have to happen together, and the order matters:
///
/// 1. `furniture` is updated, so the change survives a restart.
/// 2. The live ItemDefinition is MUTATED IN PLACE. Every placed Item holds a
///    reference to the same definition object, so mutating it updates every
///    copy in every loaded room at once. This is also why `:update items` is
///    the wrong tool here - ItemDataManager.Init() clears and rebuilds the
///    dictionary, leaving already-placed items pointing at orphaned
///    definitions that no longer change with the database.
/// 3. Every loaded room holding one regenerates its map, because walkability
///    and seating are baked into Gamemap at generation time, not read per
///    step.
///
/// Then the new record goes to everyone online: the client keeps its own copy
/// of these flags in FurnitureData, and nitro-renderer will not compute a walk
/// target for a furni whose canStandOn/canSitOn/canLayOn are all false. A
/// server-only change would leave a tile the server paths to but that nobody
/// can reach by clicking the furni.
///
/// Size, sprite and floor/wall are deliberately NOT editable: those come from
/// the furni's artwork, and changing them here would make the server block
/// tiles the sprite never covers.
/// </summary>
internal class RpFurniFunctionEvent : IPacketEvent
{
    private const int MaximumHeight = 4000; // hundredths; the stack-height widget's ceiling is 40
    private const int MaximumModes = 128;
    private const int MaximumNameLength = 56; // `furniture`.`public_name` is varchar(56)

    private readonly IDatabase _database;
    private readonly IRoomManager _roomManager;
    private readonly IGameClientManager _clientManager;
    private readonly ICatalogManager _catalogManager;
    private readonly IItemDataManager _itemDataManager;

    public RpFurniFunctionEvent(IDatabase database, IRoomManager roomManager, IGameClientManager clientManager,
        ICatalogManager catalogManager, IItemDataManager itemDataManager)
    {
        _database = database;
        _roomManager = roomManager;
        _clientManager = clientManager;
        _catalogManager = catalogManager;
        _itemDataManager = itemDataManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var definitionId = packet.ReadInt();
        var publicName = (packet.ReadString() ?? string.Empty).Trim();
        var walkable = packet.ReadBool();
        var walkMask = SanitiseMask(packet.ReadString());
        var seat = packet.ReadBool();
        var stackable = packet.ReadBool();
        var heightHundredths = Math.Clamp(packet.ReadInt(), 0, MaximumHeight);
        var adjustableHeights = ParseDoubleList(packet.ReadString());
        var heightMarker = packet.ReadBool();
        var interactionName = (packet.ReadString() ?? string.Empty).Trim().ToLowerInvariant();
        var modes = Math.Clamp(packet.ReadInt(), 1, MaximumModes);
        var effectId = Math.Max(0, packet.ReadInt());
        var behaviourData = Math.Max(0, packet.ReadInt());
        var vendingIds = ParseIntList(packet.ReadString());
        // pixelrp: 0 edits the DEFINITION, hotel-wide, as this tool always has.
        // Anything else is the id of the one placed item to scope the change to.
        // Read unconditionally - the whole record has to come off the wire.
        var scopeItemId = (uint)Math.Max(0, packet.ReadInt());
        // pixelrp: last on the wire so a client from before it existed still
        // parses. Such a client never sent it, so the furni keeps what it has
        // rather than being reset - resolved below, once the definition is.
        bool? layAcrossSent = packet.HasDataRemaining() ? packet.ReadBool() : null;

        var habbo = session.GetHabbo();
        if (habbo == null || !habbo.Permissions.HasCommand("rp_furni_function"))
            return Task.CompletedTask;

        var room = habbo.CurrentRoom;
        if (room == null)
            return Task.CompletedTask;

        // Resolved through the room the caller is standing in, so a client
        // cannot name an arbitrary definition id it never had in front of it.
        var item = room.GetRoomItemHandler().GetFloor.FirstOrDefault(x => x.Definition?.Id == (uint)definitionId)
                   ?? room.GetRoomItemHandler().GetWall.FirstOrDefault(x => x.Definition?.Id == (uint)definitionId);
        var definition = item?.Definition;
        if (definition == null)
            return Task.CompletedTask;

        // pixelrp: scoped to one item. Handled here, before any of the
        // definition-wide work below, because almost none of it applies: no
        // shared object is mutated, no other room is touched, and nothing is
        // broadcast - the client mirrors none of these five fields.
        if (scopeItemId > 0)
        {
            ApplyToSingleItem(session, room, scopeItemId, definition, interactionName, modes, effectId,
                behaviourData, vendingIds);
            return Task.CompletedTask;
        }

        // An unrecognised name would silently become InteractionType.None and
        // quietly strip whatever the furni did, so it is rejected instead.
        if (string.IsNullOrEmpty(interactionName))
            interactionName = "default";
        var interactionType = InteractionTypes.GetTypeFromString(interactionName);
        if (interactionType == InteractionType.None && interactionName != "default")
            return Task.CompletedTask;

        // A blank name would leave the furni nameless everywhere it is listed,
        // so it keeps the one it has rather than being cleared.
        if (string.IsNullOrEmpty(publicName))
            publicName = definition.PublicName ?? string.Empty;
        if (publicName.Length > MaximumNameLength)
            publicName = publicName.Substring(0, MaximumNameLength);

        var layAcross = layAcrossSent ?? definition.LayAcross;
        var height = heightHundredths / 100d;
        var changes = new List<(string Field, string Old, string New)>();
        void Track(string field, string oldValue, string newValue)
        {
            if (oldValue != newValue)
                changes.Add((field, oldValue, newValue));
        }

        Track("public_name", definition.PublicName ?? string.Empty, publicName);
        Track("is_walkable", Bit(definition.Walkable), Bit(walkable));
        Track("walk_mask", definition.WalkMask ?? string.Empty, walkMask);
        Track("can_sit", Bit(definition.IsSeat), Bit(seat));
        Track("can_stack", Bit(definition.Stackable), Bit(stackable));
        Track("stack_height", Num(definition.Height), Num(height));
        Track("height_adjustable", Join(definition.AdjustableHeights), Join(adjustableHeights));
        Track("height_marker", Bit(definition.HeightMarker), Bit(heightMarker));
        Track("lay_across", Bit(definition.LayAcross), Bit(layAcross));
        Track("interaction_type", definition.InteractionTypeName ?? "default", interactionName);
        Track("interaction_modes_count", definition.Modes.ToString(), modes.ToString());
        Track("effect_id", definition.EffectId.ToString(), effectId.ToString());
        Track("behaviour_data", definition.BehaviourData.ToString(), behaviourData.ToString());
        Track("vending_ids", Join(definition.VendingIds), Join(vendingIds));

        if (changes.Count == 0)
            return Task.CompletedTask;

        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `furniture` SET `public_name` = @publicName, `is_walkable` = @walkable, `walk_mask` = @walkMask, `can_sit` = @seat, " +
                              "`can_stack` = @stackable, `stack_height` = @height, `height_adjustable` = @adjustable, `height_marker` = @heightMarker, `lay_across` = @layAcross, " +
                              "`interaction_type` = @interaction, `interaction_modes_count` = @modes, " +
                              "`effect_id` = @effect, `behaviour_data` = @behaviour, `vending_ids` = @vending " +
                              "WHERE `id` = @definitionId LIMIT 1");
            dbClient.AddParameter("publicName", publicName);
            dbClient.AddParameter("walkable", Bit(walkable));
            dbClient.AddParameter("walkMask", walkMask);
            dbClient.AddParameter("seat", Bit(seat));
            dbClient.AddParameter("stackable", Bit(stackable));
            dbClient.AddParameter("height", height);
            dbClient.AddParameter("adjustable", adjustableHeights.Count > 0 ? Join(adjustableHeights) : "0");
            dbClient.AddParameter("heightMarker", Bit(heightMarker));
            dbClient.AddParameter("layAcross", Bit(layAcross));
            dbClient.AddParameter("interaction", interactionName);
            dbClient.AddParameter("modes", modes);
            dbClient.AddParameter("effect", effectId);
            dbClient.AddParameter("behaviour", behaviourData);
            dbClient.AddParameter("vending", vendingIds.Count > 0 ? Join(vendingIds) : "0");
            dbClient.AddParameter("definitionId", definitionId);
            dbClient.RunQuery();

            // The catalog serves its listing name from catalog_items, not from
            // the furniture row, so a rename that stopped at `furniture` would
            // leave the shop still selling the old name.
            //
            // Except for wallpaper, floor and landscape, where catalog_name is
            // NOT a display name: the catalog composers read the id out of it
            // with CatalogName.Split('_')[2], so a name without two underscores
            // throws IndexOutOfRangeException and takes the whole page down.
            // Those keep the key they were given.
            if (!IsStructuredCatalogName(definition.InteractionType))
            {
                dbClient.SetQuery("UPDATE `catalog_items` SET `catalog_name` = @publicName WHERE `item_id` = @itemIdText");
                dbClient.AddParameter("publicName", publicName);
                dbClient.AddParameter("itemIdText", definitionId.ToString());
                dbClient.RunQuery();
            }

            foreach (var change in changes)
            {
                dbClient.SetQuery("INSERT INTO `rp_furni_function_log` (`definition_id`, `item_name`, `user_id`, " +
                                  "`username`, `field`, `old_value`, `new_value`) VALUES (@definitionId, @itemName, " +
                                  "@userId, @username, @field, @oldValue, @newValue)");
                dbClient.AddParameter("definitionId", definitionId);
                dbClient.AddParameter("itemName", definition.ItemName ?? string.Empty);
                dbClient.AddParameter("userId", habbo.Id);
                dbClient.AddParameter("username", habbo.Username ?? string.Empty);
                dbClient.AddParameter("field", change.Field);
                dbClient.AddParameter("oldValue", change.Old);
                dbClient.AddParameter("newValue", change.New);
                dbClient.RunQuery();
            }
        }

        // In place, not a reload - see the class note.
        definition.PublicName = publicName;
        definition.Walkable = walkable;
        definition.WalkMask = walkMask;
        definition.IsSeat = seat;
        definition.Stackable = stackable;
        definition.Height = height;
        definition.AdjustableHeights = adjustableHeights;
        definition.HeightMarker = heightMarker;
        definition.LayAcross = layAcross;
        definition.InteractionType = interactionType;
        definition.InteractionTypeName = interactionName;
        definition.Modes = modes;
        definition.EffectId = effectId;
        definition.BehaviourData = behaviourData;
        definition.VendingIds = vendingIds;

        // The catalog is served from memory, so the pages hold their own copy
        // of the name and would go on showing the old one until a reload.
        if (!IsStructuredCatalogName(definition.InteractionType))
        {
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
        }

        // Remembered so a room can re-send this record to anyone entering it:
        // the client reads names and walkability out of gamedata on disk, which
        // this edit does not touch.
        _itemDataManager.EditedDefinitions.Add(definition.Id);

        foreach (var loaded in _roomManager.GetRooms())
        {
            var handler = loaded?.GetRoomItemHandler();
            if (handler == null)
                continue;
            if (!handler.GetFloor.Any(x => x.Definition?.Id == definition.Id) &&
                !handler.GetWall.Any(x => x.Definition?.Id == definition.Id))
                continue;
            loaded.GetGameMap().GenerateMaps();
            // The music panel's "is there a jukebox here" flag is only rebroadcast
            // when one is placed or removed, so a furni that BECAME a jukebox - or
            // stopped being one - would not show up until the next room entry.
            loaded.GetJukeboxManager()?.BroadcastState();
        }

        _clientManager.SendPacket(new RpFurniFunctionComposer(definition));
        return Task.CompletedTask;
    }

    /// <summary>
    /// pixelrp: the same edit, scoped to one placed furni.
    ///
    /// Only the five fields the client does not mirror - see
    /// 136_ItemFunctionOverrides. Name, walkability and the rest are silently
    /// NOT applied here rather than refused, because the window disables them
    /// for a scoped edit and a crafted packet carrying them should change
    /// nothing rather than half-apply.
    /// </summary>
    private void ApplyToSingleItem(GameClient session, Room room, uint itemId, ItemDefinition definition,
        string interactionName, int modes, int effectId, int behaviourData, List<int> vendingIds)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;

        var item = room.GetRoomItemHandler().GetFloor.FirstOrDefault(x => x.Id == itemId)
                   ?? room.GetRoomItemHandler().GetWall.FirstOrDefault(x => x.Id == itemId);
        // Resolved through the room the caller is standing in, same as the
        // definition is, and it has to BE the furni they opened the window on.
        if (item == null || item.Definition?.Id != definition.Id)
            return;

        // Laying is derived client-side from the interaction type, so scoping
        // one to or from a laying type cannot be expressed per item - the client
        // would apply it to every copy or to none.
        var wasLaying = ItemFunctionOverrides.IsLayingType(item.Definition.InteractionTypeName);
        var willLay = ItemFunctionOverrides.IsLayingType(interactionName);
        if (wasLaying || willLay)
        {
            session.SendWhisper("Bed and tent behaviours cannot be set on a single furni - the client reads laying per furni type.");
            return;
        }

        ItemFunctionOverrides.Set(itemId, ItemFunctionOverrides.FieldInteractionType, interactionName, habbo.Id);
        ItemFunctionOverrides.Set(itemId, ItemFunctionOverrides.FieldModes, modes.ToString(), habbo.Id);
        ItemFunctionOverrides.Set(itemId, ItemFunctionOverrides.FieldEffectId, effectId.ToString(), habbo.Id);
        ItemFunctionOverrides.Set(itemId, ItemFunctionOverrides.FieldBehaviourData, behaviourData.ToString(), habbo.Id);
        ItemFunctionOverrides.Set(itemId, ItemFunctionOverrides.FieldVendingIds,
            vendingIds.Count > 0 ? Join(vendingIds) : "0", habbo.Id);

        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("INSERT INTO `rp_furni_function_log` (`definition_id`, `item_name`, `user_id`, " +
                              "`username`, `field`, `old_value`, `new_value`) VALUES (@definitionId, @itemName, " +
                              "@userId, @username, 'scopedBehaviour', @oldValue, @newValue)");
            dbClient.AddParameter("definitionId", (int)definition.Id);
            dbClient.AddParameter("itemName", definition.ItemName ?? string.Empty);
            dbClient.AddParameter("userId", habbo.Id);
            dbClient.AddParameter("username", habbo.Username ?? string.Empty);
            dbClient.AddParameter("oldValue", $"item {itemId}: {item.Definition.InteractionTypeName}");
            dbClient.AddParameter("newValue", $"item {itemId}: {interactionName}");
            dbClient.RunQuery();
        }

        // Re-clone from the SHARED definition rather than from the item's own,
        // so clearing a field really clears it instead of leaving the previous
        // override in place.
        if (PlusEnvironment.Game.ItemManager.Items.TryGetValue(definition.Id, out var shared))
            item.Definition = shared;
        item.HasOwnDefinition = false;
        ItemFunctionOverrides.Apply(item, ItemFunctionOverrides.ForItems(new[] { itemId }).GetValueOrDefault(itemId));

        // Walkability is baked into the map at generation time. Nothing here
        // changes it today, but a behaviour can imply a seat, and regenerating
        // one room is cheap next to a tile nobody can path to.
        room.GetGameMap().GenerateMaps();
    }

    /// A mask the map cannot read is worse than none - it would punch holes in
    /// a wall on a shape nobody intended - so anything that is not exactly the
    /// footprint's worth of 0s and 1s, or is all zeroes (which says nothing the
    /// furni's own flag does not), comes back as no mask at all.
    private static string SanitiseMask(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        raw = raw.Trim();
        if (raw.Length > 64 || raw.Any(c => c != '0' && c != '1') || raw.All(c => c == '0'))
            return string.Empty;
        return raw;
    }

    /// These three carry an id inside catalog_name rather than a display name -
    /// the catalog composers pull it out with Split('_')[2] - so renaming one
    /// would corrupt the key and crash the page it sits on.
    private static bool IsStructuredCatalogName(InteractionType type) =>
        type is InteractionType.Wallpaper or InteractionType.Floor or InteractionType.Landscape;

    private static string Bit(bool value) => value ? "1" : "0";

    private static string Num(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Join(IEnumerable<double> values) =>
        string.Join(",", values.Select(x => x.ToString("0.####", CultureInfo.InvariantCulture)));

    private static string Join(IEnumerable<int> values) => string.Join(",", values);

    private static List<int> ParseIntList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "0")
            return new List<int>(0);
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var value) ? value : -1)
            .Where(value => value >= 0)
            .Take(64)
            .ToList();
    }

    private static List<double> ParseDoubleList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "0")
            return new List<double>(0);
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : -1)
            .Where(value => value >= 0 && value <= MaximumHeight / 100d)
            .Take(32)
            .ToList();
    }
}
