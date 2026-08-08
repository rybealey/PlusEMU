using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.Utilities;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

internal class GetRoomEntryDataEvent : IPacketEvent
{
    private readonly IQuestManager _questManager;

    public GetRoomEntryDataEvent(IQuestManager questManager)
    {
        _questManager = questManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        var roomUserManager = room.GetRoomUserManager();
        // Only add the avatar on a genuine first entry. A duplicated GetRoomEntryData
        // - the login room-forward processed twice by the client under network latency
        // - arrives with the user already in the room from the first entry.
        // AddAvatarToRoom then returns false (its first guard), and the old code
        // REMOVED the user, deleting the avatar that had already rendered: the "sprite
        // flashes then vanishes once the room loads" bug, reproducible only under prod
        // latency (locally: 1 room-ready / 2 add-user / 0 remove; on prod: 2 / 4 / 2).
        // For the redundant request, skip the add/remove but still send the full room
        // state so the client's rebuilt view renders the avatar. Genuine add failures
        // (broken room/model) still tear down as before.
        if (roomUserManager.GetRoomUserByHabbo(session.GetHabbo().Id) == null
            && !roomUserManager.AddAvatarToRoom(session))
        {
            roomUserManager.RemoveUserFromRoom(session, false);
            return Task.CompletedTask;
        }
        room.SendObjects(session);
        if (session.GetHabbo().Messenger != null)
            session.GetHabbo().Messenger.NotifyChangesToFriends();
        if (session.GetHabbo().HabboStats.QuestId > 0)
            _questManager.QuestReminder(session, session.GetHabbo().HabboStats.QuestId);
        session.Send(new RoomEntryInfoComposer(room.RoomId, room.CheckRights(session, true)));
        session.Send(new RoomVisualizationSettingsComposer(room.WallThickness, room.FloorThickness, Convert.ToBoolean(room.Hidewall)));
        var user = roomUserManager.GetRoomUserByHabbo(session.GetHabbo().Username);
        if (user != null && session.GetHabbo().PetId == 0) room.SendPacket(new UserChangeComposer(user, false));
        session.Send(new RoomEventComposer(room, room.Promotion));
        if (room.GetWired() != null)
            room.GetWired().TriggerEvent(WiredBoxType.TriggerRoomEnter, session.GetHabbo());
        if (UnixTimestamp.GetNow() < session.GetHabbo().FloodTime && session.GetHabbo().FloodTime != 0)
            session.Send(new FloodControlComposer((int)session.GetHabbo().FloodTime - (int)UnixTimestamp.GetNow()));
        return Task.CompletedTask;
    }
}