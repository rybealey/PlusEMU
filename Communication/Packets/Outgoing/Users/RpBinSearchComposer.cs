using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the searcher's own bin search card (HabboHotel.Rooms.BinSearch) -
/// whether one is running, the bin's sprite id for its icon, how long is left
/// and how long a search takes, in milliseconds. Sent when a search starts
/// and again, not searching, when it ends however it ends.
/// </summary>
public class RpBinSearchComposer : IServerPacket
{
    private readonly bool _searching;
    private readonly int _spriteId;
    private readonly int _leftMs;
    private readonly int _totalMs;

    public uint MessageId => ServerPacketHeader.RpBinSearchComposer;

    public RpBinSearchComposer(bool searching, int spriteId = 0, int leftMs = 0, int totalMs = 0)
    {
        _searching = searching;
        _spriteId = spriteId;
        _leftMs = leftMs;
        _totalMs = totalMs;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_searching);
        packet.WriteInteger(_spriteId);
        packet.WriteInteger(_leftMs);
        packet.WriteInteger(_totalMs);
    }
}
