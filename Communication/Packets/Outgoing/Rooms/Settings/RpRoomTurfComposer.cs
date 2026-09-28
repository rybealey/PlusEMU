using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Settings;

/// <summary>
/// pixelrp turfs: whether this room is a turf, and which gang holds it - sent
/// beside RpRoomZoneComposer, which still carries safe/unsafe. A separate
/// packet rather than a longer RpRoomZoneComposer, because that one is parsed
/// inside the patched renderer and this one is parsed in the client's own
/// source (see RpTurfMessages.ts).
/// </summary>
public class RpRoomTurfComposer : IServerPacket
{
    private readonly int _roomId;
    private readonly bool _isTurf;
    private readonly int _ownerGangId;
    private readonly string _ownerName;

    public uint MessageId => ServerPacketHeader.RpRoomTurfComposer;

    public RpRoomTurfComposer(uint roomId, bool isTurf, int ownerGangId, string ownerName)
    {
        _roomId = (int)roomId;
        _isTurf = isTurf;
        _ownerGangId = ownerGangId;
        _ownerName = ownerName ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_roomId);
        packet.WriteBoolean(_isTurf);
        packet.WriteInteger(_ownerGangId);
        packet.WriteString(_ownerName);
    }

    /// <summary>The composer for a room as it stands now.</summary>
    public static RpRoomTurfComposer For(HabboHotel.Rooms.Room room) =>
        new(room.Id, room.IsTurf, room.IsTurf ? HabboHotel.Gangs.TurfManager.OwnerOf(room.Id) : 0,
            room.IsTurf ? HabboHotel.Gangs.TurfManager.OwnerName(room.Id) : "");
}
