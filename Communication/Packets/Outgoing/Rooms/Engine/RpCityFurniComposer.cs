using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// pixelrp: every furni type in the city's always-loaded rooms (KeyRooms), as
/// sprite ids - floor, then wall - sent once at login. The client downloads
/// any it does not have yet in the background, so a first visit to a city
/// room draws its furni instead of grey placeholders while they download.
/// </summary>
public class RpCityFurniComposer : IServerPacket
{
    private readonly ICollection<int> _floorSpriteIds;
    private readonly ICollection<int> _wallSpriteIds;

    public uint MessageId => ServerPacketHeader.RpCityFurniComposer;

    public RpCityFurniComposer(ICollection<int> floorSpriteIds, ICollection<int> wallSpriteIds)
    {
        _floorSpriteIds = floorSpriteIds;
        _wallSpriteIds = wallSpriteIds;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_floorSpriteIds.Count);
        foreach (var spriteId in _floorSpriteIds)
            packet.WriteInteger(spriteId);
        packet.WriteInteger(_wallSpriteIds.Count);
        foreach (var spriteId in _wallSpriteIds)
            packet.WriteInteger(spriteId);
    }
}
