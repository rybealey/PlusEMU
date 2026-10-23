using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Avatar;

public class SleepComposer : IServerPacket, IUnitPacket
{
    private readonly RoomUser _user;
    private readonly bool _isSleeping;
    public uint MessageId => ServerPacketHeader.SleepComposer;

    public IServerPacket? WithoutUnits(ISet<int> hidden) => hidden.Contains(_user.VirtualId) ? null : this;

    public SleepComposer(RoomUser user, bool isSleeping)
    {
        _user = user;
        _isSleeping = isSleeping;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_user.VirtualId);
        packet.WriteBoolean(_isSleeping);
    }
}