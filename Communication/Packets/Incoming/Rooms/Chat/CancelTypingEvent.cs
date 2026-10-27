using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Chat;

public class CancelTypingEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().InRoom)
            return Task.CompletedTask;
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        // pixelrp: by id (the room's index), not by searching names
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null)
            return Task.CompletedTask;
        // pixelrp (Twist, 2026-10-27): a stop is never limited - one dropped
        // would leave the bubble up - so it is passed on only when the room was
        // told of a start, and a stream of stops alone sends nothing.
        if (!user.IsTyping)
            return Task.CompletedTask;
        user.IsTyping = false;
        session.GetHabbo().CurrentRoom.SendPacket(new UserTypingComposer(user.VirtualId, false));
        return Task.CompletedTask;
    }
}