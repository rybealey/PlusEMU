using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Handshake;

public class UserRightsComposer : IServerPacket
{
    private readonly int _clubLevel;
    private readonly int _rank;
    private readonly bool _isAmbassador;

    public uint MessageId => ServerPacketHeader.UserRightsComposer;

    public UserRightsComposer(int clubLevel, int rank, bool isAmbassador)
    {
        _clubLevel = clubLevel;
        _rank = rank;
        _isAmbassador = isAmbassador;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_clubLevel); // 2 = VIP, 0 = none
        packet.WriteInteger(_rank);
        packet.WriteBoolean(_isAmbassador);
    }
}
