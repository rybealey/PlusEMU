using Plus.HabboHotel.GameClients;

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
        // A click on the tile you stand on does nothing - but only when you ARE
        // standing. While walking, RoomUser.X/Y is the current step's FROM tile
        // (server truth runs a step behind the drawn avatar), i.e. the tile you
        // are leaving, so this dropped every "turn back" click: the player
        // clicked where they had just been and nothing happened. For a walker
        // it now goes through as an ordinary redirect - a U-turn at the next
        // step, like any change of direction. (While waiting for the beat, a
        // click on your own tile cancels the walk before it starts.)
        if (moveX == user.X && moveY == user.Y
            && !Plus.HabboHotel.Rooms.Movement.MovementV2Bridge.IsWalkingOrWaiting(user))
            return Task.CompletedTask;
        if (user.RidingHorse)
        {
            var horse = room.GetRoomUserManager().GetRoomUserByVirtualId(user.HorseId);
            if (horse != null)
                horse.MoveTo(moveX, moveY);
        }
        user.MoveTo(moveX, moveY);
        return Task.CompletedTask;
    }
}