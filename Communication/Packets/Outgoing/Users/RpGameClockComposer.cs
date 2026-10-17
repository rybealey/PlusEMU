using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the city's clock (HabboHotel.Weather.GameClock) - how far into
/// the 90-minute cycle the server is right now, the cycle's length, and the
/// staff pin (-1 for none). The client runs the clock on from the moment it
/// arrives, so a player's own computer clock being wrong never moves the
/// time; it is sent at login and again whenever staff pin or free it.
/// </summary>
public class RpGameClockComposer : IServerPacket
{
    private readonly int _cycleOffsetMs;
    private readonly int _cycleMs;
    private readonly int _pinnedMinutes;

    public uint MessageId => ServerPacketHeader.RpGameClockComposer;

    public RpGameClockComposer(int cycleOffsetMs, int cycleMs, int pinnedMinutes)
    {
        _cycleOffsetMs = cycleOffsetMs;
        _cycleMs = cycleMs;
        _pinnedMinutes = pinnedMinutes;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_cycleOffsetMs);
        packet.WriteInteger(_cycleMs);
        packet.WriteInteger(_pinnedMinutes);
    }
}
