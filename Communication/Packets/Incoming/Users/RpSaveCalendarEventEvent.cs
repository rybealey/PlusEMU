using Dapper;
using Plus.HabboHotel.Calendar;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.Utilities;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: staff post (id 0) or edit a calendar event. Validated, stored,
/// then the calendar is re-sent to every online client.
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

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (id <= 0)
                connection.Execute(
                    "INSERT INTO `rp_events` (`title`, `description`, `starts_at`, `ends_at`, `room_id`, `colour`, `host_name`, `created_by`, `created_at`) " +
                    "VALUES (@title, @description, @startsAt, @endsAt, @roomId, @colour, @hostName, @createdBy, @now)",
                    new { title, description, startsAt, endsAt, roomId, colour, hostName, createdBy = habbo.Id, now = (int)UnixTimestamp.GetNow() });
            else
                connection.Execute(
                    "UPDATE `rp_events` SET `title` = @title, `description` = @description, `starts_at` = @startsAt, `ends_at` = @endsAt, " +
                    "`room_id` = @roomId, `colour` = @colour, `host_name` = @hostName WHERE `id` = @id",
                    new { id, title, description, startsAt, endsAt, roomId, colour, hostName });
        }

        CalendarUtility.BroadcastCalendar();
        return Task.CompletedTask;
    }
}
