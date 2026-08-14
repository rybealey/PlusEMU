using Plus.Communication.Attributes;
using Plus.Communication.Packets.Incoming.Rooms;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Navigator;

[StaffOnly]
internal class GoToHotelViewEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        // pixelrp: hotel view is staff-only. The stock client has no leave-room UI,
        // so an in-room exit request from a non-staff user can only come from a
        // modified client — ignore it. (RoomPacketEvent already no-ops when the
        // user is not in a room, so error/doorbell flows never reach this.)
        if (!session.GetHabbo().IsStaff) return Task.CompletedTask;
        room.GetRoomUserManager()?.RemoveUserFromRoom(session, true);
        return Task.CompletedTask;
    }
}