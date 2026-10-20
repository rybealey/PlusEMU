using Plus.Communication.Packets.Outgoing.Navigator;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Incoming.Navigator;

internal class GetGuestRoomEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roomId = packet.ReadUInt();
        if (!RoomFactory.TryGetData(roomId, out var data))
            return Task.CompletedTask;
        var enter = packet.ReadInt() == 1;
        var forward = packet.ReadInt() == 1;
        if (forward && !enter && EnteredDirectly(session, roomId))
            return Task.CompletedTask;
        session.Send(new GetGuestRoomResultComposer(session, data, enter, forward));
        return Task.CompletedTask;
    }

    /// <summary>
    /// pixelrp: a forward request is the client on its way into a room - a
    /// RoomForward from the server (a summon, a 911 call, jail, a group's
    /// room), or a Builder or above going somewhere from the navigator. Answered
    /// with the room's details, the client would then tear down the room it is
    /// in and ask to enter: a second round trip, on a blank screen. From inside
    /// a room, with nothing at the door to stop them (RoomDoor), they are moved
    /// straight in instead, as an arrow moves them: the old room stays on screen
    /// until the new one is there. Only where OpenFlatConnectionEvent would let
    /// them in - the navigator (Builder up, and below the staff line not while
    /// TravelGuard holds them), or the room the server sent them to - so this is
    /// no way into anywhere else. A held Builder is left to that event, which
    /// says why and keeps them in their room.
    /// </summary>
    private static bool EnteredDirectly(GameClient session, uint roomId)
    {
        var habbo = session.GetHabbo();
        if (habbo == null || habbo.CurrentRoom == null || habbo.CurrentRoom.RoomId == roomId)
            return false;
        var sentHere = roomId == habbo.AuthorizedRoomEntryId || (habbo.IsTeleporting && roomId == habbo.TeleportingRoomId);
        if (!sentHere && (!habbo.IsBuilder || (!habbo.IsStaff && TravelGuard.Refusal(session) != null)))
            return false;
        if (!PlusEnvironment.Game.RoomManager.TryLoadRoom(roomId, out var room) || !RoomDoor.LetsIn(session, room))
            return false;
        habbo.PrepareRoom(roomId, "");
        return true;
    }
}