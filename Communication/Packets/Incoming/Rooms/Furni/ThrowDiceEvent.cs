using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

internal class ThrowDiceEvent : IPacketEvent
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
        var hasRights = room.CheckRights(session, false, true);
        var request = packet.ReadInt();
        // pixelrp: dice that walk them up to the table are them walking off (OwnWalk).
        OwnWalk.Use(session, room, () => item.Interactor.OnTrigger(session, item, request, hasRights));
        return Task.CompletedTask;
    }
}