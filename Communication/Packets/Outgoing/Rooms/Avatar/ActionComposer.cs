using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Avatar;

public class ActionComposer : IServerPacket, IUnitPacket
{
    private readonly int _virtualId;
    private readonly int _action;
    public uint MessageId => ServerPacketHeader.ActionComposer;

    public IServerPacket? WithoutUnits(ISet<int> hidden) => hidden.Contains(_virtualId) ? null : this;

    public ActionComposer(int virtualId, int action)
    {
        _virtualId = virtualId;
        _action = action;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_virtualId);
        packet.WriteInteger(_action);
    }
}