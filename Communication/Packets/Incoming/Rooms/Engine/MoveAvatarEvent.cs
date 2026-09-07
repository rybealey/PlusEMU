using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

internal class MoveAvatarEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!session.GetHabbo().InRoom)
            return Task.CompletedTask;
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null || !user.CanWalk)
            return Task.CompletedTask;
        // A player-initiated path ends any staff-forced :walk patrol or :wander.
        user.ForcedWalkAxis = null;
        user.ForcedWalkRandom = false;
        var moveX = packet.ReadInt();
        var moveY = packet.ReadInt();
        if (moveX == user.X && moveY == user.Y)
            return Task.CompletedTask;
        if (user.RidingHorse)
        {
            var horse = room.GetRoomUserManager().GetRoomUserByVirtualId(user.HorseId);
            if (horse != null)
                horse.MoveTo(moveX, moveY);
        }
        // pixelrp police escort: an escorted suspect is walked in the same
        // breath as their captor, exactly as a horse is walked with its rider
        // above. Both walks are then scheduled together and the client
        // interpolates them side by side, which is what keeps the suspect
        // pinned in front instead of trailing a step behind.
        PoliceState.OnCaptorWalkRequest(room, user, moveX, moveY);
        user.MoveTo(moveX, moveY);
        return Task.CompletedTask;
    }
}