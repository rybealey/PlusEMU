using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Settings;

/// <summary>
/// pixelrp: the room's roleplay zone type (safe zones freeze the passive
/// countdown). Sent alongside RoomSettingsDataComposer when the settings
/// window opens, and back to the saver on RpRoomZoneSaveEvent.
/// </summary>
public class RpRoomZoneComposer : IServerPacket
{
    private readonly int _roomId;
    private readonly bool _isSafeZone;

    public uint MessageId => ServerPacketHeader.RpRoomZoneComposer;

    public RpRoomZoneComposer(uint roomId, bool isSafeZone)
    {
        _roomId = (int)roomId;
        _isSafeZone = isSafeZone;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_roomId);
        packet.WriteBoolean(_isSafeZone);
    }
}
