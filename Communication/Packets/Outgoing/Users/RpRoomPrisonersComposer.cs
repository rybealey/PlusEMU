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
public class RpRoomPrisonersComposer : IServerPacket, IUnitPacket
{
    private readonly List<int> _virtualIds;

    public uint MessageId => ServerPacketHeader.RpRoomPrisonersComposer;

    public RpRoomPrisonersComposer(List<int> virtualIds) => _virtualIds = virtualIds;

    // pixelrp :invisible: the list without a prisoner who is out of sight -
    // one brought in by a moderator to talk. Still sent when that leaves it
    // empty: it replaces what the viewer knew.
    public IServerPacket? WithoutUnits(ISet<int> hidden) =>
        _virtualIds.Any(hidden.Contains) ? new RpRoomPrisonersComposer(_virtualIds.Where(id => !hidden.Contains(id)).ToList()) : this;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_virtualIds.Count);
        foreach (var virtualId in _virtualIds)
            packet.WriteInteger(virtualId);
    }
}
