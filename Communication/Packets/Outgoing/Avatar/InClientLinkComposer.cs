using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Avatar;

public class InClientLinkComposer : IServerPacket
{
    private readonly string _link;

    public uint MessageId => ServerPacketHeader.InClientLinkComposer;

    public InClientLinkComposer(string link)
    {
        _link = link;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_link);
    }
}
