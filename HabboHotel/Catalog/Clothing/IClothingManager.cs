namespace Plus.HabboHotel.Catalog.Clothing;

public interface IClothingManager
{
    ICollection<ClothingItem> GetClothingAllParts { get; }
    void Init();
    bool TryGetClothing(int itemId, out ClothingItem clothing);
    /// <summary>pixelrp: claims one copy of a limited edition; returns the
    /// edition number, or 0 when sold out.</summary>
    int TrySellLtd(ClothingItem clothing);
}
