using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Chat;

public class StartTypingEvent : IPacketEvent
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
        // pixelrp (Twist, 2026-10-27): once per start. The client sends one per
        // line; a start limited away (PacketLimits.Typing) never sets this, so
        // its stop is not passed on either (CancelTypingEvent).
        if (user.IsTyping)
            return Task.CompletedTask;
        user.IsTyping = true;
        session.GetHabbo().CurrentRoom.SendPacket(new UserTypingComposer(user.VirtualId, true));
        return Task.CompletedTask;
    }
}