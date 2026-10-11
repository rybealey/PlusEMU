using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp gym (GymState): your own workout panel.
///
/// Showing (standing on a machine): the stat ("strength" / "stamina"), the
/// machine's name, the level, the seconds left to the next level and how many
/// that level takes in all (both 0 at level 10), the membership's online
/// seconds left, and why training is paused ("" while it counts). Hidden: only
/// the membership, which the Gym bot's menu shows.
///
/// Sent on a change and every 30 seconds while showing - the client counts
/// both clocks down by itself in between, the workout's only while unpaused.
/// </summary>
public class RpGymComposer : IServerPacket
{
    private readonly bool _showing;
    private readonly string _stat;
    private readonly string _machine;
    private readonly int _level;
    private readonly int _secondsLeft;
    private readonly int _secondsForLevel;
    private readonly int _membershipSeconds;
    private readonly string _pause;

    public uint MessageId => ServerPacketHeader.RpGymComposer;

    public RpGymComposer(bool showing, string stat, string machine, int level, int secondsLeft, int secondsForLevel,
        int membershipSeconds, string pause)
    {
        _showing = showing;
        _stat = stat;
        _machine = machine;
        _level = level;
        _secondsLeft = secondsLeft;
        _secondsForLevel = secondsForLevel;
        _membershipSeconds = membershipSeconds;
        _pause = pause;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_showing);
        packet.WriteString(_stat);
        packet.WriteString(_machine);
        packet.WriteInteger(_level);
        packet.WriteInteger(_secondsLeft);
        packet.WriteInteger(_secondsForLevel);
        packet.WriteInteger(_membershipSeconds);
        packet.WriteString(_pause);
    }
}
