using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// pixelrp (Twist, 2026-10-28): the furni types of the room behind an arrow a
/// player is walking to (ArrowAhead), as sprite ids - the room id, then floor,
/// then wall - so the client downloads the ones it does not have while they
/// walk, and the room draws its furni as it arrives instead of grey
/// placeholders. The same shape as RpCityFurniComposer, with the room first.
/// </summary>
public class RpRoomFurniAheadComposer : IServerPacket
{
    private readonly uint _roomId;
    private readonly ICollection<int> _floorSpriteIds;
    private readonly ICollection<int> _wallSpriteIds;

    public uint MessageId => ServerPacketHeader.RpRoomFurniAheadComposer;

    public RpRoomFurniAheadComposer(uint roomId, ICollection<int> floorSpriteIds, ICollection<int> wallSpriteIds)
    {
        _roomId = roomId;
        _floorSpriteIds = floorSpriteIds;
        _wallSpriteIds = wallSpriteIds;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger((int)_roomId);
        packet.WriteInteger(_floorSpriteIds.Count);
        foreach (var spriteId in _floorSpriteIds)
            packet.WriteInteger(spriteId);
        packet.WriteInteger(_wallSpriteIds.Count);
        foreach (var spriteId in _wallSpriteIds)
            packet.WriteInteger(spriteId);
    }
}
