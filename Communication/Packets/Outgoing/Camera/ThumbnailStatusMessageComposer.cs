using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Camera;

public class ThumbnailStatusMessageComposer : IServerPacket
{
    private readonly bool _ok;
    public uint MessageId => ServerPacketHeader.ThumbnailStatusMessageComposer;

    public ThumbnailStatusMessageComposer(bool ok)
    {
        _ok = ok;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_ok);
        packet.WriteBoolean(false); // render limit hit
    }
}
