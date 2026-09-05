using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>pixelrp: one player's birthday, keyed by user id: month + day (0/0 = not set). No year exists.</summary>
public class RpBirthdayComposer : IServerPacket
{
    private readonly int _userId;
    private readonly int _month;
    private readonly int _day;

    public uint MessageId => ServerPacketHeader.RpBirthdayComposer;

    public RpBirthdayComposer(int userId, int month, int day)
    {
        _userId = userId;
        _month = month;
        _day = day;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_userId);
        packet.WriteInteger(_month);
        packet.WriteInteger(_day);
    }
}
