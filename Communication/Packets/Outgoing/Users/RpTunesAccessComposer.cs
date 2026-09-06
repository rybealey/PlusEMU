using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>pixelrp: whether this player may skip the station's song and remove anyone's request from the phone's Tunes app (staff).</summary>
public class RpTunesAccessComposer : IServerPacket
{
    private readonly bool _canManage;

    public uint MessageId => ServerPacketHeader.RpTunesAccessComposer;

    public RpTunesAccessComposer(bool canManage)
    {
        _canManage = canManage;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_canManage ? 1 : 0);
    }
}
