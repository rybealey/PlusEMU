using Dapper;
using Plus.HabboHotel.Calendar;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notifications;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.Utilities;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: staff post (id 0) or edit a calendar event. Validated, stored,
/// then the calendar is re-sent to every online client.
///
/// A new event notifies the hotel. So does an edit, but only one that changes
/// where or when it is - retitling or rewording an event is not news, and a
/// player who has been told "moved to 22:00" should not then hear about the
/// description being tidied up. Events that have already finished never
/// notify anyone.
/// </summary>
internal class RpSaveCalendarEventEvent : IPacketEvent
{
    private readonly IWordFilterManager _wordFilterManager;

    public RpSaveCalendarEventEvent(IWordFilterManager wordFilterManager)
    {
        _wordFilterManager = wordFilterManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var title = _wordFilterManager.CheckMessage(packet.ReadString()).Trim();
        var description = _wordFilterManager.CheckMessage(packet.ReadString()).Trim();
        var startsAt = packet.ReadInt();
        var endsAt = packet.ReadInt();
        var roomId = Math.Max(0, packet.ReadInt());
        var colour = CalendarUtility.CleanColour(packet.ReadString());
        var hostName = packet.ReadString().Trim();
        // trailing, optional: 1 = all-day (the client sends the day's bounds as the times)
        var allDay = packet.HasDataRemaining() && packet.ReadInt() == 1;

        var habbo = session.GetHabbo();
        if (!CalendarUtility.IsStaff(habbo))
            return Task.CompletedTask;
        if (title.Length == 0 || title.Length > CalendarUtility.MaxTitle || endsAt <= startsAt || startsAt <= 0)
        {
            session.SendWhisper("An event needs a title and an end after its start.");
            return Task.CompletedTask;
        }
        if (description.Length > CalendarUtility.MaxDescription) description = description.Substring(0, CalendarUtility.MaxDescription);
        if (hostName.Length == 0) hostName = habbo.Username;
        if (hostName.Length > CalendarUtility.MaxHost) hostName = hostName.Substring(0, CalendarUtility.MaxHost);

        var isNew = id <= 0;
        // For an edit: what it said before, so we can tell whether this is
        // worth anyone's attention and what it moved from.
        var wasStartsAt = 0;
        var moved = false;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (isNew)
                id = connection.QuerySingle<int>(
                    "INSERT INTO `rp_events` (`title`, `description`, `starts_at`, `ends_at`, `all_day`, `room_id`, `colour`, `host_name`, `created_by`, `created_at`) " +
                    "VALUES (@title, @description, @startsAt, @endsAt, @allDay, @roomId, @colour, @hostName, @createdBy, @now); SELECT LAST_INSERT_ID();",
                    new { title, description, startsAt, endsAt, allDay = allDay ? 1 : 0, roomId, colour, hostName, createdBy = habbo.Id, now = (int)UnixTimestamp.GetNow() });
            else
            {
                var before = connection.QueryFirstOrDefault<BeforeRow>(
                    "SELECT `starts_at` AS StartsAt, `ends_at` AS EndsAt, `room_id` AS RoomId, `all_day` AS AllDay FROM `rp_events` WHERE `id` = @id LIMIT 1", new { id });
                if (before != null)
                {
                    wasStartsAt = before.StartsAt;
                    moved = before.StartsAt != startsAt || before.EndsAt != endsAt || before.RoomId != roomId || before.AllDay != allDay;
                }
                connection.Execute(
                    "UPDATE `rp_events` SET `title` = @title, `description` = @description, `starts_at` = @startsAt, `ends_at` = @endsAt, `all_day` = @allDay, " +
                    "`room_id` = @roomId, `colour` = @colour, `host_name` = @hostName WHERE `id` = @id",
                    new { id, title, description, startsAt, endsAt, allDay = allDay ? 1 : 0, roomId, colour, hostName });
            }
        }

        CalendarUtility.BroadcastCalendar();

        if (endsAt >= (int)UnixTimestamp.GetNow())
        {
            if (isNew)
                NotificationUtility.PushAll(NotificationUtility.Calendar, "event_new", title, habbo.Username, id, exceptUserId: habbo.Id);
            else if (moved)
                // A start that actually shifted travels as the old time, so the
                // banner can say what it moved from; 0 means something else
                // about the event changed.
                NotificationUtility.PushAll(NotificationUtility.Calendar, "event_changed", title, habbo.Username, id,
                    (wasStartsAt != startsAt) ? wasStartsAt : 0, exceptUserId: habbo.Id);
        }
        return Task.CompletedTask;
    }

    /// <summary>The parts of an event whose change is worth a notification.</summary>
    private class BeforeRow
    {
        public int StartsAt { get; set; }
        public int EndsAt { get; set; }
        public int RoomId { get; set; }
        public bool AllDay { get; set; }
    }
}
