using Dapper;
using Microsoft.Extensions.Logging;
using Plus.Database;

namespace Plus.HabboHotel.Catalog.Clothing;

public class ClothingManager : IClothingManager
{
    private readonly IDatabase _database;
    private readonly ILogger<ClothingManager> _logger;
    // Replaced wholesale on every Init so readers never see a half-built
    // shelf (the clothing store and ProcessFigure enumerate it constantly).
    private Dictionary<int, ClothingItem> _clothing;

    public ClothingManager(IDatabase database, ILogger<ClothingManager> logger)
    {
        _database = database;
        _logger = logger;
        _clothing = new();
    }

    public ICollection<ClothingItem> GetClothingAllParts => _clothing.Values;

    // pixelrp: synchronous and fully guarded. This used to be async void; an
    // exception there is unhandled and takes the whole emulator down, so a
    // bad row or a schema slip must only cost the shelf, never the hotel.
    public void Init()
    {
        var loaded = new Dictionary<int, ClothingItem>();
        try
        {
            using var connection = _database.Connection();
            var rows = connection.Query<ClothingRow>(
                "SELECT `id` AS Id, `clothing_name` AS ClothingName, `clothing_parts` AS PartIds, `display_name` AS DisplayName, " +
                "`price` AS Price, `ltd_total` AS LtdTotal, `ltd_sold` AS LtdSold FROM `catalog_clothing`").ToList();
            // the purchasable_clothing furni that used to sell each set: its
            // catalog icon is the token's art in the backpack
            var icons = new Dictionary<int, string>();
            try
            {
                foreach (var icon in connection.Query<IconRow>(
                    "SELECT `behaviour_data` AS ClothingId, MIN(`item_name`) AS ItemName FROM `furniture` " +
                    "WHERE `interaction_type` = 'purchasable_clothing' GROUP BY `behaviour_data`"))
                    icons.TryAdd(icon.ClothingId, icon.ItemName ?? string.Empty);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Clothing store: could not read furni icons");
            }
            foreach (var row in rows)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(row.PartIds))
                        continue;
                    icons.TryGetValue(row.Id, out var icon);
                    loaded[row.Id] = new(row.Id, row.ClothingName, row.PartIds, row.DisplayName, row.Price, row.LtdTotal, row.LtdSold, icon);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Clothing store: skipping catalog_clothing row {Id} ({Name})", row.Id, row.ClothingName);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Clothing store: failed to load catalog_clothing");
            return;
        }
        _clothing = loaded;
        _logger.LogInformation("Clothing store: {Count} sets loaded", loaded.Count);
    }

    public bool TryGetClothing(int itemId, out ClothingItem clothing) => _clothing.TryGetValue(itemId, out clothing);

    /// <summary>pixelrp: claims one copy of a limited edition. The UPDATE is
    /// guarded by the stock count so two buyers racing for the last copy
    /// cannot both get it. Returns the edition number claimed, or 0 when the
    /// edition is sold out (or the claim failed).</summary>
    public int TrySellLtd(ClothingItem clothing)
    {
        if (clothing == null || !clothing.IsLtd)
            return 0;
        try
        {
            using var connection = _database.Connection();
            var claimed = connection.Execute(
                "UPDATE `catalog_clothing` SET `ltd_sold` = `ltd_sold` + 1 WHERE `id` = @id AND `ltd_sold` < `ltd_total`",
                new { id = clothing.Id });
            if (claimed == 0)
                return 0;
            var edition = connection.QuerySingle<int>("SELECT `ltd_sold` FROM `catalog_clothing` WHERE `id` = @id", new { id = clothing.Id });
            clothing.LtdSold = edition;
            return edition;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Clothing store: LTD claim failed for {Id}", clothing.Id);
            return 0;
        }
    }
}

// Dapper rows: plain property classes so the nullable columns map cleanly.
internal sealed class ClothingRow
{
    public int Id { get; set; }
    public string ClothingName { get; set; }
    public string PartIds { get; set; }
    public string DisplayName { get; set; }
    public int Price { get; set; }
    public int LtdTotal { get; set; }
    public int LtdSold { get; set; }
}

internal sealed class IconRow
{
    public int ClothingId { get; set; }
    public string ItemName { get; set; }
}
