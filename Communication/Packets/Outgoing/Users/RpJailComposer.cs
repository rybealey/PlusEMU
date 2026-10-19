using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp jail: the prisoner's own countdown. Seconds left and the length of
/// the whole sentence; 0 and 0 means free. Sent to the prisoner only - when
/// they are sentenced, each time they enter a room while serving (so a relog
/// or the walk to the cell shows it), and when they are released. The client
/// counts down on its own between these (RpJailView), so no tick is sent.
/// Seconds rather than a release time, so the client's clock cannot skew it.
/// Appended last: where they stand on a jury trial (JailState.Trial*), for the
/// Request a Trial card (RpJuryTrialView), and whether the clock is stopped -
/// it is, in the Courthouse during their trial - so the countdown holds still.
/// </summary>
public class RpJailComposer : IServerPacket
{
    private readonly int _secondsLeft;
    private readonly int _sentenceSeconds;
    private readonly int _trial;
    private readonly bool _paused;

    public uint MessageId => ServerPacketHeader.RpJailComposer;

    public RpJailComposer(int secondsLeft, int sentenceSeconds, int trial = 0, bool paused = false)
    {
        _secondsLeft = Math.Max(0, secondsLeft);
        _sentenceSeconds = Math.Max(0, sentenceSeconds);
        _trial = trial;
        _paused = paused;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_secondsLeft);
        packet.WriteInteger(_sentenceSeconds);
        packet.WriteInteger(_trial);
        packet.WriteBoolean(_paused);
    }
}
