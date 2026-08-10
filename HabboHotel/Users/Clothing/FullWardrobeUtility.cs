using Plus.HabboHotel.Catalog.Clothing;
using Plus.HabboHotel.Users.Clothing.Parts;

namespace Plus.HabboHotel.Users.Clothing;

public static class FullWardrobeUtility
{
    /// <summary>
    /// The clothing parts to advertise to the client via FigureSetIdsComposer.
    /// Full-wardrobe users get every purchasable part unioned with their own;
    /// everyone else gets only what they own.
    /// </summary>
    public static ICollection<ClothingParts> GetVisibleClothingParts(Habbo habbo, IClothingManager clothingManager)
    {
        if (!habbo.HasFullWardrobe)
            return habbo.Clothing.GetClothingParts;
        var parts = new Dictionary<int, ClothingParts>();
        foreach (var owned in habbo.Clothing.GetClothingParts)
            parts.TryAdd(owned.PartId, owned);
        foreach (var clothing in clothingManager.GetClothingAllParts)
            foreach (var partId in clothing.PartIds)
                parts.TryAdd(partId, new(0, partId, clothing.ClothingName));
        return parts.Values;
    }
}
