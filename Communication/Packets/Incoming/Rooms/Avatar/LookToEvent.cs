using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.Communication.Packets.Incoming.Rooms.Avatar;

internal class LookToEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null)
            return Task.CompletedTask;
        if (user.IsAsleep)
            return Task.CompletedTask;
        user.UnIdle();
        var x = packet.ReadInt();
        var y = packet.ReadInt();
        if (x == user.X && y == user.Y || user.IsWalking || user.RidingHorse)
            return Task.CompletedTask;
        // pixelrp police escort: a suspect in custody faces the way their
        // captor faces, and only the captor's records may write it.
        if (HabboHotel.Rooms.Chat.Commands.User.Police.PoliceState.IsBeingEscorted(user.UserId))
            return Task.CompletedTask;
        var rot = Rotation.Calculate(user.X, user.Y, x, y);
        user.SetRot(rot, false);
        user.UpdateNeeded = true;
        // pixelrp police escort: the suspect is kept one tile in FRONT, so when
        // the captor turns on the spot the suspect has to move round to the new
        // front and face the same way. Packet thread, no locks held.
        HabboHotel.Rooms.Chat.Commands.User.Police.PoliceState.OnCaptorTurn(room, user, rot);
        if (user.RidingHorse)
        {
            var horse = session.GetHabbo().CurrentRoom.GetRoomUserManager().GetRoomUserByVirtualId(user.HorseId);
            if (horse != null)
            {
                horse.SetRot(rot, false);
                horse.UpdateNeeded = true;
            }
        }
        return Task.CompletedTask;
    }
}