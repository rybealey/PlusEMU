using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp jail: the prisoner's own countdown. Seconds left and the length of
/// the whole sentence; 0 and 0 means free. Sent to the prisoner only - when
/// they are sentenced, each time they enter a room while serving (so a relog
/// or the walk to the cell shows it), and when they are released. The client
/// counts down on its own between these (RpJailView), so no tick is sent.
/// Seconds rather than a release time, so the client's clock cannot skew it.
/// </summary>
public class RpJailComposer : IServerPacket
{
    private readonly int _secondsLeft;
    private readonly int _sentenceSeconds;

    public uint MessageId => ServerPacketHeader.RpJailComposer;

    public RpJailComposer(int secondsLeft, int sentenceSeconds)
    {
        _secondsLeft = Math.Max(0, secondsLeft);
        _sentenceSeconds = Math.Max(0, sentenceSeconds);
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_secondsLeft);
        packet.WriteInteger(_sentenceSeconds);
    }
}
