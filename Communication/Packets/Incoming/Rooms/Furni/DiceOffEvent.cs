using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

internal class DiceOffEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        var item = room.GetRoomItemHandler().GetItem(packet.ReadUInt());
        if (item == null)
            return Task.CompletedTask;
        if (KnockedOut.Refuse(session))
            return Task.CompletedTask;
        var hasRights = room.CheckRights(session);
        // pixelrp: furni that walks them up to it is them walking off (OwnWalk).
        OwnWalk.Use(session, room, () => item.Interactor.OnTrigger(session, item, -1, hasRights));
        return Task.CompletedTask;
    }
}