using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.HabboHotel.Items;

public class ItemDefinition
{
    public uint Id { get; set; }
    public int SpriteId { get; set; }
    public string ItemName { get; set; }
    public string PublicName { get; set; }
    public ItemType Type { get; set; }

    /// <summary>
    /// Raw furniture sprite-type char from the database: <c>s</c> floor, <c>i</c> wall,
    /// <c>e</c> effect, <c>r</c> bot, <c>b</c> badge, <c>p</c> pet. <see cref="Type"/>
    /// collapses this to Floor/Wall, which loses the product category the catalog
    /// purchase dispatcher needs to route bots, effects, badges and pets correctly.
    /// </summary>
    public string ProductType { get; set; }

    public FurniCategory Category { get; set; } = FurniCategory.Default;
    public int Width { get; set; }
    public int Length { get; set; }
    public double Height { get; set; }
    public bool Stackable { get; set; }
    public bool Walkable { get; set; }
    public bool IsSeat { get; set; }
    public bool AllowEcotronRecycle { get; set; }
    public bool AllowTrade { get; set; }
    public bool AllowMarketplaceSell { get; set; }
    public bool AllowGift { get; set; }
    public bool AllowInventoryStack { get; set; }

    /// TODO @80O: Convert to string so plugins can add new interactions.
    public InteractionType InteractionType { get; set; }
    public int BehaviourData { get; set; }
    public int Modes { get; set; }
    public List<int> VendingIds { get; set; }
    public List<double> AdjustableHeights { get; set; }
    public int EffectId { get; set; }

    /// TODO @80O: Should be removed, use unique interaction name instead.
    public WiredBoxType WiredType { get; set; }

    /// TODO @80O: This is dumb, remove it.
    public bool IsRare { get; set; }

    /// TODO @80O: I think this can be removed. Seems useless and unclear what its supposed to do.
    public bool ExtraRot { get; set; }
}
