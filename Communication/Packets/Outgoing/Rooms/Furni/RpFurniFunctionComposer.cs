using System.Globalization;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni;

/// <summary>
/// pixelrp: a furni DEFINITION's behaviour, as the Function window edits it.
///
/// One shape serves both uses. Opening the window sends it to the one client
/// that asked, with the context it needs to show what it is editing (name,
/// size, how many copies are out there); applying a change sends the same
/// record to everyone online, because the client keeps its own copy of these
/// flags in FurnitureData and would otherwise go on using the old ones.
///
/// That second use is not optional. nitro-renderer's RoomObjectEventHandler
/// refuses to compute a walk target for a furni whose canStandOn, canSitOn and
/// canLayOn are all false - so a server-side walkability change that never
/// reaches the client leaves a tile the server will happily path to but that
/// players cannot reach by clicking the furni itself.
///
/// `placedCopies` / `roomCount` are context for the warning the window shows
/// before applying, and are zero on the broadcast - only the client that asked
/// needs them, and counting them for every recipient would be wasteful.
/// </summary>
public class RpFurniFunctionComposer : IServerPacket
{
    private readonly ItemDefinition _definition;
    private readonly int _placedCopies;
    private readonly int _roomCount;

    public uint MessageId => ServerPacketHeader.RpFurniFunctionComposer;

    public RpFurniFunctionComposer(ItemDefinition definition, int placedCopies = 0, int roomCount = 0)
    {
        _definition = definition;
        _placedCopies = placedCopies;
        _roomCount = roomCount;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger((int)_definition.Id);
        // The client keys its FurnitureData by SPRITE id, not definition id -
        // the two happen to match for furni we imported ourselves, but not for
        // the official library, so both travel.
        packet.WriteInteger(_definition.SpriteId);
        packet.WriteString(_definition.ItemName ?? string.Empty);
        packet.WriteString(_definition.PublicName ?? string.Empty);
        packet.WriteString(_definition.ProductType ?? "s");
        packet.WriteInteger(_definition.Width);
        packet.WriteInteger(_definition.Length);

        packet.WriteBoolean(_definition.Walkable);
        packet.WriteString(_definition.WalkMask ?? string.Empty);
        packet.WriteBoolean(_definition.IsSeat);
        packet.WriteBoolean(_definition.Stackable);
        // Height rides as hundredths, the same wire format the stack-height
        // widget already speaks, so nothing here has to agree on float encoding.
        packet.WriteInteger((int)Math.Round(_definition.Height * 100));
        packet.WriteString(string.Join(",", _definition.AdjustableHeights.Select(
            height => height.ToString("0.####", CultureInfo.InvariantCulture))));
        // Display only: whether the tile cursor may show its raised ring over
        // this furni. The client is the only reader - see
        // 111_FurniHeightMarker.sql for what the ring actually is.
        packet.WriteBoolean(_definition.HeightMarker);

        packet.WriteString(_definition.InteractionTypeName ?? "default");
        packet.WriteInteger(_definition.Modes);
        packet.WriteInteger(_definition.EffectId);
        packet.WriteInteger(_definition.BehaviourData);
        packet.WriteString(string.Join(",", _definition.VendingIds));

        packet.WriteInteger(_placedCopies);
        packet.WriteInteger(_roomCount);
    }
}
