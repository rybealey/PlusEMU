using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: everything a player is sent on walking into a room - its shape,
/// everybody in it, its furni - sent by the server the moment the room change
/// is made (Habbo.EnterRoom), straight behind RoomReady.
///
/// WHY. The client used to have to ask for it (GetRoomEntryData) when RoomReady
/// reached it, and the room came back with the answer: a whole round trip -
/// the player's ping - of blank screen on every room change. At 30ms nobody
/// sees it; at 250ms it is a quarter of a second, every time.
///
/// THE CLIENT STILL ASKS. Nitro sends GetRoomEntryData on every RoomReady;
/// GetRoomEntryDataEvent answers it only when the room was NOT already sent
/// (Habbo.EntrySentRoomId) - otherwise it would all arrive twice, which at
/// high ping costs as much again.
/// </summary>
public static class RoomEntry
{
    public static void Send(GameClient session, Room room, IQuestManager questManager)
    {
        var roomUserManager = room.GetRoomUserManager();
        // The room's shape first. The client builds the room the moment it has
        // it - the end of the blank screen - so it does not wait on the database
        // lookups AddAvatarToRoom makes for the entering player (their job, their
        // inventory's weapon, a turf room's gang). Everything that used to arrive
        // before the room existed and was dropped (this player's own avatar,
        // effect and hand item) is now applied instead, and the copies
        // SendObjects sends after it change nothing: the client skips an avatar
        // it already has (RoomEngine.addRoomObjectUser).
        room.SendHeightmaps(session);
        // Only add the avatar on a genuine first entry. A duplicated entry - the
        // login room-forward processed twice by the client under network latency -
        // arrives with the user already in the room from the first one.
        // AddAvatarToRoom then returns false (its first guard), and the old code
        // REMOVED the user, deleting the avatar that had already rendered: the "sprite
        // flashes then vanishes once the room loads" bug, reproducible only under prod
        // latency (locally: 1 room-ready / 2 add-user / 0 remove; on prod: 2 / 4 / 2).
        // For the redundant entry, skip the add/remove but still send the full room
        // state so the client's rebuilt view renders the avatar. Genuine add failures
        // (broken room/model) still tear down as before.
        if (roomUserManager.GetRoomUserByHabbo(session.GetHabbo().Id) == null
            && !roomUserManager.AddAvatarToRoom(session))
        {
            roomUserManager.RemoveUserFromRoom(session, false);
            return;
        }
        room.SendObjects(session);
        if (session.GetHabbo().Messenger != null)
            session.GetHabbo().Messenger.NotifyChangesToFriends();
        if (session.GetHabbo().HabboStats.QuestId > 0)
            questManager.QuestReminder(session, session.GetHabbo().HabboStats.QuestId);
        session.Send(new RoomEntryInfoComposer(room.RoomId, room.CheckRights(session, true)));
        session.Send(new RoomVisualizationSettingsComposer(room.WallThickness, room.FloorThickness, Convert.ToBoolean(room.Hidewall)));
        var user = roomUserManager.GetRoomUserByHabbo(session.GetHabbo().Username);
        if (user != null && session.GetHabbo().PetId == 0) room.SendPacket(new UserChangeComposer(user, false));
        if (room.GetWired() != null)
            room.GetWired().TriggerEvent(WiredBoxType.TriggerRoomEnter, session.GetHabbo());
        if (UnixTimestamp.GetNow() < session.GetHabbo().FloodTime && session.GetHabbo().FloodTime != 0)
            session.Send(new FloodControlComposer((int)session.GetHabbo().FloodTime - (int)UnixTimestamp.GetNow()));
    }
}
