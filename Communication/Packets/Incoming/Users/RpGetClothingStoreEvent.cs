using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Catalog.Clothing;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: the Clothing Store (or the Backpack, naming a token) wants the shelf.</summary>
internal class RpGetClothingStoreEvent : IPacketEvent
{
    private readonly IClothingManager _clothingManager;

    public RpGetClothingStoreEvent(IClothingManager clothingManager)
    {
        _clothingManager = clothingManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null)
            return Task.CompletedTask;
        session.Send(new RpClothingStoreComposer(_clothingManager.GetClothingAllParts));
        return Task.CompletedTask;
    }
}
