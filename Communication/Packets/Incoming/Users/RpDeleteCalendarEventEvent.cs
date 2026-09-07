using Dapper;
using Plus.HabboHotel.Calendar;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notifications;
using Plus.Utilities;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: staff delete a calendar event; everyone's calendar is
/// re-sent, and the hotel is told an event they may have been counting on is
/// off. The title comes from the row before it goes, since the notification
/// has to name an event that no longer exists.</summary>
internal class RpDeleteCalendarEventEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        if (!CalendarUtility.IsStaff(session.GetHabbo()) || id <= 0)
            return Task.CompletedTask;
        string title;
        int endsAt;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            var row = connection.QueryFirstOrDefault<CancelledRow>(
                "SELECT `title` AS Title, `ends_at` AS EndsAt FROM `rp_events` WHERE `id` = @id LIMIT 1", new { id });
            if (row == null)
                return Task.CompletedTask;
            title = row.Title;
            endsAt = row.EndsAt;
            connection.Execute("DELETE FROM `rp_events` WHERE `id` = @id", new { id });
        }
        CalendarUtility.BroadcastCalendar();
        // Clearing out an event that has already finished is housekeeping.
        if (endsAt >= (int)UnixTimestamp.GetNow())
            NotificationUtility.PushAll(NotificationUtility.Calendar, "event_cancelled", title, session.GetHabbo().Username, id, exceptUserId: session.GetHabbo().Id);
        return Task.CompletedTask;
    }

    private class CancelledRow
    {
        public string Title { get; set; } = "";
        public int EndsAt { get; set; }
    }
}
