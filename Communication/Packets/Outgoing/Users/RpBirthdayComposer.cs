using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>pixelrp: the viewer's own birthday, month + day (0/0 = not set). No year exists.</summary>
public class RpBirthdayComposer : IServerPacket
{
    private readonly int _month;
    private readonly int _day;

    public uint MessageId => ServerPacketHeader.RpBirthdayComposer;

    public RpBirthdayComposer(int month, int day)
    {
        _month = month;
        _day = day;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_month);
        packet.WriteInteger(_day);
    }
}
