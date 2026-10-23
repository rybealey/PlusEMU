using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Settings;

/// <summary>
/// pixelrp: a room's floor opacity, 0 (see-through) to 100 (solid)
/// (RoomFloorOpacity). Sent behind the room's shape on walking in (RoomEntry),
/// to everyone in the room when it changes, and beside RoomSettingsDataComposer
/// for the Room tool's slider. Its parser is in client source
/// (RpFloorOpacityMessages.ts).
/// </summary>
public class RpRoomFloorOpacityComposer : IServerPacket
{
    private readonly int _roomId;
    private readonly int _opacity;

    public uint MessageId => ServerPacketHeader.RpRoomFloorOpacityComposer;

    public RpRoomFloorOpacityComposer(uint roomId, int opacity)
    {
        _roomId = (int)roomId;
        _opacity = opacity;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_roomId);
        packet.WriteInteger(_opacity);
    }
}
