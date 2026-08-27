using Plus.HabboHotel.DiamondsStore;
using Plus.HabboHotel.GameClients;
using Plus.Communication.Packets.Outgoing.Users;

namespace Plus.Communication.Packets.Incoming.Users;

internal class GetDiamondsStoreEvent : IPacketEvent
{
    private readonly IDiamondsStoreManager _storeManager;

    public GetDiamondsStoreEvent(IDiamondsStoreManager storeManager) => _storeManager = storeManager;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.Send(new DiamondsStoreComposer(_storeManager.Items));
        return Task.CompletedTask;
    }
}
