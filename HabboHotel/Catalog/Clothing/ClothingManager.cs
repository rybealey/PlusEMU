using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Catalog.Clothing;

public class ClothingManager : IClothingManager
{
    private readonly IDatabase _database;
    private readonly Dictionary<int, ClothingItem> _clothing;

    public ClothingManager(IDatabase database)
    {
        _database = database;
        _clothing = new();
    }

    public ICollection<ClothingItem> GetClothingAllParts => _clothing.Values;

    public async void Init()
    {
        _clothing.Clear();
        using var connection = _database.Connection();
        // pixelrp: the store columns ride along, plus the classname of the
        // purchasable_clothing furni that used to sell this set (its catalog
        // icon is the LTD token's art in the backpack). Read into a property
        // class, not a positional record - display_name and the furni name are
        // nullable and Dapper cannot map those positionally.
        var data = await connection.QueryAsync<ClothingRow>(
            "SELECT c.`id` AS Id, c.`clothing_name` AS ClothingName, c.`clothing_parts` AS PartIds, c.`display_name` AS DisplayName, " +
            "c.`price` AS Price, c.`ltd_total` AS LtdTotal, c.`ltd_sold` AS LtdSold, " +
            "(SELECT MIN(f.`item_name`) FROM `furniture` f WHERE f.`interaction_type` = 'purchasable_clothing' AND f.`behaviour_data` = c.`id`) AS Icon " +
            "FROM `catalog_clothing` c");
        foreach (var row in data)
        {
            if (string.IsNullOrWhiteSpace(row.PartIds))
                continue;
            _clothing.Add(row.Id, new(row.Id, row.ClothingName, row.PartIds, row.DisplayName, row.Price, row.LtdTotal, row.LtdSold, row.Icon));
        }
    }

    public bool TryGetClothing(int itemId, out ClothingItem clothing) => _clothing.TryGetValue(itemId, out clothing);

    /// <summary>pixelrp: claims one copy of a limited edition. The UPDATE is
    /// guarded by the stock count so two buyers racing for the last copy
    /// cannot both get it. Returns the edition number claimed, or 0 when the
    /// edition is sold out.</summary>
    public int TrySellLtd(ClothingItem clothing)
    {
        if (clothing == null || !clothing.IsLtd)
            return 0;
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
}

// Dapper row for catalog_clothing joined to its furni; a plain property
// class so the nullable display_name / furni name map cleanly.
internal sealed class ClothingRow
{
    public int Id { get; set; }
    public string ClothingName { get; set; }
    public string PartIds { get; set; }
    public string DisplayName { get; set; }
    public int Price { get; set; }
    public int LtdTotal { get; set; }
    public int LtdSold { get; set; }
    public string Icon { get; set; }
}
