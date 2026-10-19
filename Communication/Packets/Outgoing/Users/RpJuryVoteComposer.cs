using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp jury trials: a juror's vote card (JuryTrials) - open with the
/// defendant, the seconds left to vote, this juror's own vote (0 none, 1
/// guilty, 2 not guilty) and how many of the jurors have voted; or closed.
/// Sent to each juror in court when the vote opens, again whenever a juror
/// votes, and closed at the verdict or when the trial is called off. Seconds
/// rather than a time, so the client's clock cannot skew it.
/// </summary>
public class RpJuryVoteComposer : IServerPacket
{
    private readonly bool _open;
    private readonly string _name;
    private readonly string _look;
    private readonly string _gender;
    private readonly int _secondsLeft;
    private readonly int _myVote;
    private readonly int _voted;
    private readonly int _jurors;

    public uint MessageId => ServerPacketHeader.RpJuryVoteComposer;

    /// <summary>The card taken away.</summary>
    public RpJuryVoteComposer() : this(false, "", "", "M", 0, 0, 0, 0) { }

    public RpJuryVoteComposer(bool open, string name, string look, string gender, int secondsLeft, int myVote, int voted, int jurors)
    {
        _open = open;
        _name = name ?? "";
        _look = look ?? "";
        _gender = gender ?? "M";
        _secondsLeft = Math.Max(0, secondsLeft);
        _myVote = myVote;
        _voted = voted;
        _jurors = jurors;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_open);
        if (!_open)
            return;
        packet.WriteString(_name);
        packet.WriteString(_look);
        packet.WriteString(_gender);
        packet.WriteInteger(_secondsLeft);
        packet.WriteInteger(_myVote);
        packet.WriteInteger(_voted);
        packet.WriteInteger(_jurors);
    }
}
