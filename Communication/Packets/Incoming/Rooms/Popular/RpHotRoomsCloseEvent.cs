using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Popular;

/// <summary>pixelrp: the :hotrooms window was closed - stop sending it updates (HotRooms).</summary>
internal class RpHotRoomsCloseEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        HotRooms.Close(session);
        return Task.CompletedTask;
    }
}
