using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Camera;

public class CameraStorageUrlMessageComposer : IServerPacket
{
    private readonly string _url;
    public uint MessageId => ServerPacketHeader.CameraStorageUrlMessageComposer;

    public CameraStorageUrlMessageComposer(string url)
    {
        _url = url;
    }

    public void Compose(IOutgoingPacket packet) => packet.WriteString(_url);
}
