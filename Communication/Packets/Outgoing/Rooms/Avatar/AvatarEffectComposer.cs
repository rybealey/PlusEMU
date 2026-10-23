using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Avatar;

public class AvatarEffectComposer : IServerPacket, IUnitPacket
{
    private readonly int _playerId;
    private readonly int _effectId;
    public uint MessageId => ServerPacketHeader.AvatarEffectComposer;

    public IServerPacket? WithoutUnits(ISet<int> hidden) => hidden.Contains(_playerId) ? null : this;

    public AvatarEffectComposer(int playerId, int effectId)
    {
        _playerId = playerId;
        _effectId = effectId;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_playerId);
        packet.WriteInteger(_effectId);
        packet.WriteInteger(0);
    }
}