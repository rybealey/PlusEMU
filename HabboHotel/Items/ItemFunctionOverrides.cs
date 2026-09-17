using System.Data;
using System.Globalization;

namespace Plus.HabboHotel.Items;

/// <summary>
/// pixelrp: behaviour scoped to ONE placed furni rather than every copy.
///
/// The trick that makes this cheap: Item.Definition is a per-item reference, and
/// the ~128 places that ask what a furni does all read it THROUGH the item. So
/// an overridden item is given a private CLONE of the shared definition with the
/// overrides applied, and every one of those readers is correct without being
/// touched. Nothing asks "is this overridden?" - it simply reads a definition
/// that already says the right thing.
///
/// The clone is per item and never shared, so mutating it cannot leak into
/// other copies - which is exactly the property the hotel-wide Function tool
/// relies on in reverse (it mutates the shared object so every copy changes).
///
/// Only the fields the client does not mirror are overridable. See
/// 136_ItemFunctionOverrides for the reasoning; the short version is that
/// FurnitureData is keyed by furni class, so a per-item name or walkability
/// would desync the client.
/// </summary>
public static class ItemFunctionOverrides
{
    public const string FieldInteractionType = "interaction_type";
    public const string FieldModes = "modes";
    public const string FieldEffectId = "effect_id";
    public const string FieldBehaviourData = "behaviour_data";
    public const string FieldVendingIds = "vending_ids";

    /// <summary>The fields an override may name. Anything else is refused.</summary>
    public static readonly string[] Fields =
    {
        FieldInteractionType, FieldModes, FieldEffectId, FieldBehaviourData, FieldVendingIds
    };

    /// <summary>
    /// Laying is DERIVED client-side from the interaction type, so scoping one
    /// of these to a single item cannot be expressed - the client would apply
    /// it to every copy or to none.
    /// </summary>
    public static bool IsLayingType(string interactionTypeName) =>
        interactionTypeName is "bed" or "tent_small";

    /// <summary>Every override for a set of items, as {itemId: {field: value}}.</summary>
    public static Dictionary<uint, Dictionary<string, string>> ForItems(IEnumerable<uint> itemIds)
    {
        var result = new Dictionary<uint, Dictionary<string, string>>();
        var ids = itemIds?.Distinct().ToList();
        if (ids == null || ids.Count == 0)
            return result;

        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        // Built rather than parameterised because the list is unbounded and
        // every element is a uint straight off a primary key - there is no
        // string to inject through.
        dbClient.SetQuery(
            "SELECT `item_id`, `field`, `value` FROM `rp_item_function` WHERE `item_id` IN (" +
            string.Join(",", ids) + ")");
        var table = dbClient.GetTable();
        if (table == null)
            return result;

        foreach (DataRow row in table.Rows)
        {
            var itemId = Convert.ToUInt32(row["item_id"]);
            if (!result.TryGetValue(itemId, out var fields))
            {
                fields = new Dictionary<string, string>();
                result[itemId] = fields;
            }
            fields[Convert.ToString(row["field"]) ?? string.Empty] = Convert.ToString(row["value"]) ?? string.Empty;
        }
        return result;
    }

    /// <summary>
    /// Give this item its own definition carrying the overrides. A no-op when
    /// there are none, so an ordinary item keeps sharing the one object.
    /// </summary>
    public static void Apply(Item item, Dictionary<string, string> fields)
    {
        if (item?.Definition == null || fields == null || fields.Count == 0)
            return;

        var definition = Clone(item.Definition);

        foreach (var (field, value) in fields)
        {
            switch (field)
            {
                case FieldInteractionType:
                {
                    var name = (value ?? string.Empty).Trim().ToLowerInvariant();
                    if (name.Length == 0)
                        break;
                    var type = InteractionTypes.GetTypeFromString(name);
                    // An unknown name would silently become None and strip
                    // whatever the furni did, so it is left alone instead.
                    if (type == InteractionType.None && name != "default")
                        break;
                    definition.InteractionType = type;
                    definition.InteractionTypeName = name;
                    break;
                }
                case FieldModes:
                    if (int.TryParse(value, out var modes))
                        definition.Modes = Math.Clamp(modes, 1, 128);
                    break;
                case FieldEffectId:
                    if (int.TryParse(value, out var effectId))
                        definition.EffectId = Math.Max(0, effectId);
                    break;
                case FieldBehaviourData:
                    if (int.TryParse(value, out var behaviourData))
                        definition.BehaviourData = Math.Max(0, behaviourData);
                    break;
                case FieldVendingIds:
                    definition.VendingIds = ParseIntList(value);
                    break;
            }
        }

        item.Definition = definition;
        item.HasOwnDefinition = true;
    }

    /// <summary>Write one override, or clear it when the value is null.</summary>
    public static void Set(uint itemId, string field, string value, int staffId)
    {
        if (Array.IndexOf(Fields, field) < 0)
            return;

        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        if (value == null)
        {
            dbClient.SetQuery("DELETE FROM `rp_item_function` WHERE `item_id` = @itemId AND `field` = @field");
            dbClient.AddParameter("itemId", itemId);
            dbClient.AddParameter("field", field);
            dbClient.RunQuery();
            return;
        }

        dbClient.SetQuery(
            "INSERT INTO `rp_item_function` (`item_id`,`field`,`value`,`set_by`,`created_at`) " +
            "VALUES (@itemId,@field,@value,@staffId,@now) " +
            "ON DUPLICATE KEY UPDATE `value` = @value, `set_by` = @staffId, `created_at` = @now");
        dbClient.AddParameter("itemId", itemId);
        dbClient.AddParameter("field", field);
        dbClient.AddParameter("value", value);
        dbClient.AddParameter("staffId", staffId);
        dbClient.AddParameter("now", (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        dbClient.RunQuery();
    }

    /// <summary>Drop every override on an item - the "back to normal" button.</summary>
    public static void ClearAll(uint itemId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("DELETE FROM `rp_item_function` WHERE `item_id` = @itemId");
        dbClient.AddParameter("itemId", itemId);
        dbClient.RunQuery();
    }

    /// <summary>
    /// A private copy. MemberwiseClone would share the two Lists, so a per-item
    /// handitem change would reach back into the shared definition - the exact
    /// leak this whole mechanism exists to prevent.
    /// </summary>
    public static ItemDefinition Clone(ItemDefinition source) => new()
    {
        Id = source.Id,
        SpriteId = source.SpriteId,
        ItemName = source.ItemName,
        PublicName = source.PublicName,
        Type = source.Type,
        ProductType = source.ProductType,
        Category = source.Category,
        Width = source.Width,
        Length = source.Length,
        Height = source.Height,
        Stackable = source.Stackable,
        Walkable = source.Walkable,
        WalkMask = source.WalkMask,
        IsSeat = source.IsSeat,
        AllowEcotronRecycle = source.AllowEcotronRecycle,
        AllowTrade = source.AllowTrade,
        AllowMarketplaceSell = source.AllowMarketplaceSell,
        AllowGift = source.AllowGift,
        AllowInventoryStack = source.AllowInventoryStack,
        InteractionType = source.InteractionType,
        InteractionTypeName = source.InteractionTypeName,
        BehaviourData = source.BehaviourData,
        Modes = source.Modes,
        VendingIds = source.VendingIds == null ? new List<int>() : new List<int>(source.VendingIds),
        AdjustableHeights = source.AdjustableHeights == null ? new List<double>() : new List<double>(source.AdjustableHeights),
        HeightMarker = source.HeightMarker,
        EffectId = source.EffectId,
        WiredType = source.WiredType,
        IsRare = source.IsRare,
        ExtraRot = source.ExtraRot
    };

    private static List<int> ParseIntList(string raw)
    {
        var values = new List<int>();
        if (string.IsNullOrWhiteSpace(raw))
            return values;
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
                values.Add(value);
        }
        return values;
    }
}
