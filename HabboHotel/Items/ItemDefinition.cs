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

    /// <summary>
    /// Per-tile walkability override, in the furni's OWN frame so it turns with
    /// the item: `width` * `length` characters, '1' where that tile is walkable
    /// and '0' where it follows the furni's normal blocking, indexed
    /// `b * Width + a` with `a` across the width and `b` along the length.
    ///
    /// Empty - every row until someone sets one - means no override. It exists
    /// because `Walkable` is all-or-nothing across the footprint, which cannot
    /// describe an L-shaped sofa: a 2x2 that covers three tiles and leaves the
    /// inside corner as floor.
    /// </summary>
    public string WalkMask { get; set; } = string.Empty;
    public bool IsSeat { get; set; }
    public bool AllowEcotronRecycle { get; set; }
    public bool AllowTrade { get; set; }
    public bool AllowMarketplaceSell { get; set; }
    public bool AllowGift { get; set; }
    public bool AllowInventoryStack { get; set; }

    /// TODO @80O: Convert to string so plugins can add new interactions.
    public InteractionType InteractionType { get; set; }

    /// <summary>
    /// The raw `furniture`.`interaction_type` this was parsed from.
    ///
    /// InteractionTypes only maps one way - string to enum, across a 107-case
    /// switch - so without keeping the original there is no way back to the
    /// value the database holds, and the Function window needs exactly that to
    /// show what a furni is set to and to write an edit back. Carrying the
    /// string beats a second switch that would silently drift from the first.
    /// </summary>
    public string InteractionTypeName { get; set; } = "default";
    public int BehaviourData { get; set; }
    public int Modes { get; set; }
    public List<int> VendingIds { get; set; }
    public List<double> AdjustableHeights { get; set; }

    /// <summary>
    /// pixelrp: whether the tile cursor may show its raised height ring over
    /// this furni. Purely how it is DRAWN - the stacking height is unchanged
    /// either way, and the emulator never reads this. It exists so the
    /// Function window can switch off a marker that comes from the .nitro
    /// bundle's own logic and could otherwise only be removed by rebuilding
    /// the asset. See 111_FurniHeightMarker.sql.
    /// </summary>
    public bool HeightMarker { get; set; } = true;
    public int EffectId { get; set; }

    /// TODO @80O: Should be removed, use unique interaction name instead.
    public WiredBoxType WiredType { get; set; }

    /// TODO @80O: This is dumb, remove it.
    public bool IsRare { get; set; }

    /// TODO @80O: I think this can be removed. Seems useless and unclear what its supposed to do.
    public bool ExtraRot { get; set; }
}
