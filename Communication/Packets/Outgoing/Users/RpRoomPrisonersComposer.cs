using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp jail: who in this room is serving time (JailState), as the room
/// units' virtual ids - the client's roomIndex. The whole list each time,
/// so it simply replaces whatever the client had: sent to a player as they
/// come into a room, and to the whole room when a prisoner arrives there, is
/// sentenced there or is released there. The avatar menu leaves Trade off a
/// prisoner (Twist, 2026-10-18).
/// </summary>
public class RpRoomPrisonersComposer : IServerPacket
{
    private readonly List<int> _virtualIds;

    public uint MessageId => ServerPacketHeader.RpRoomPrisonersComposer;

    public RpRoomPrisonersComposer(List<int> virtualIds) => _virtualIds = virtualIds;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_virtualIds.Count);
        foreach (var virtualId in _virtualIds)
            packet.WriteInteger(virtualId);
    }
}
