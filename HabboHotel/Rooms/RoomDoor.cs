using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: whether a player in a room can be moved straight into another one
/// (Habbo.PrepareRoom) instead of being forwarded - the forward makes the
/// client ask for the room's details and then ask to enter, a round trip each
/// before the room change even starts, with the old room torn down for the
/// second. Used by follow-friend and by the client's own forward request
/// (GetGuestRoomEvent).
/// </summary>
public static class RoomDoor
{
    /// <summary>
    /// PrepareRoom's own door checks, asked ahead of it. PrepareRoom takes a
    /// player out of their room BEFORE its doorbell, password, full and ban
    /// checks, and the forward lets the client deal with those while they are
    /// still standing where they were - so only when nothing at the door can
    /// turn them away. False for a room being unloaded (:unload, a floor plan
    /// save) - the forward, which only needs its id, copes with that as it
    /// always did.
    /// </summary>
    public static bool LetsIn(GameClient session, Room room)
    {
        var habbo = session.GetHabbo();
        var users = room.GetRoomUserManager();
        var bans = room.GetBans();
        if (!habbo.InRoom || room.MDisposed || users == null || bans == null)
            return false;
        var permissions = habbo.Permissions;
        if ((room.Access == RoomAccess.Doorbell || room.Access == RoomAccess.Password)
            && !room.CheckRights(session, true, true) && !permissions.HasRight("room_enter_locked"))
            return false;
        if (users.UserCount >= room.UsersMax && !permissions.HasRight("room_enter_full") && habbo.Id != room.OwnerId)
            return false;
        return permissions.HasRight("room_ban_override") || !bans.IsBanned(habbo.Id);
    }
}
