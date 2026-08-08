using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Camera;

public class CameraPublishStatusMessageComposer : IServerPacket
{
    private readonly bool _ok;
    private readonly string _url;
    public uint MessageId => ServerPacketHeader.CameraPublishStatusMessageComposer;

    public CameraPublishStatusMessageComposer(bool ok, string url)
    {
        _ok = ok;
        _url = url;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_ok);
        packet.WriteInteger(0); // seconds to wait
        packet.WriteString(_url);
    }
}
