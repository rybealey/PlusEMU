namespace Plus.Communication.Packets;

/// <summary>
/// pixelrp :invisible: an outgoing packet about one or more room units, by
/// virtual id - their avatar, status, chat, enable, hand item. A viewer who
/// can't see an invisible unit must never receive one of these about it: the
/// client draws a nameless bubble for chat from an index it doesn't know, and
/// anyone watching their packets would see the unit is there. Room.SendPacket
/// and Room.SendPacketsBatched hand such viewers <see cref="WithoutUnits"/>.
/// </summary>
public interface IUnitPacket
{
    /// <summary>This packet as a viewer who can't see <paramref name="hidden"/>
    /// should get it: itself when it isn't about any of them, a copy without
    /// them, or null when nothing is left to send.</summary>
    IServerPacket? WithoutUnits(ISet<int> hidden);

    /// <summary><paramref name="packet"/> for a viewer who can't see
    /// <paramref name="hidden"/>; any packet that isn't about a unit goes as it
    /// is.</summary>
    static IServerPacket? Filter(IServerPacket packet, ISet<int> hidden) =>
        packet is IUnitPacket unit ? unit.WithoutUnits(hidden) : packet;
}
