namespace Plus.HabboHotel.Catalog.Clothing;

public class ClothingItem
{
    public ClothingItem(int id, string name, string partIds, string displayName = null, int price = 0, int ltdTotal = 0, int ltdSold = 0, string icon = null)
    {
        Id = id;
        ClothingName = name;
        PartIds = partIds.Split(",").Select(int.Parse).ToList();
        DisplayName = displayName ?? string.Empty;
        Price = price;
        LtdTotal = ltdTotal;
        LtdSold = ltdSold;
        Icon = icon ?? string.Empty;
    }

    public int Id { get; }
    public string ClothingName { get; }
    public List<int> PartIds { get; }

    // pixelrp Clothing Store fields (catalog_clothing columns added by 73_ClothingStore.sql)
    /// <summary>Shelf name; empty means the client tidies ClothingName.</summary>
    public string DisplayName { get; }
    /// <summary>Credits. 0 keeps the piece off the shelf.</summary>
    public int Price { get; }
    /// <summary>Copies in the edition; 0 = a regular piece, unlocked on purchase.</summary>
    public int LtdTotal { get; }
    /// <summary>Copies sold so far; bumped by <see cref="ClothingManager.TrySellLtd"/>.</summary>
    public int LtdSold { get; set; }
    /// <summary>Furni classname of the matching purchasable_clothing furni (its
    /// catalog icon is the token's art), or empty when none exists.</summary>
    public string Icon { get; }

    public bool IsLtd => LtdTotal > 0;
    public bool IsSoldOut => IsLtd && LtdSold >= LtdTotal;
    public string ShelfName => string.IsNullOrEmpty(DisplayName) ? ClothingName : DisplayName;
}
