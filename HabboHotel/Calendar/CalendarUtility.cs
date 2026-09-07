using System.Text.RegularExpressions;
using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.Utilities;

namespace Plus.HabboHotel.Calendar;

/// <summary>
/// pixelrp: the phone's Calendar app. Staff (rank 5+) post, edit and delete
/// in-game events; everyone sees them plus their friends' birthdays. Every
/// mutation re-sends the calendar to every online client (birthdays are per
/// viewer, so each client gets its own composition), so open calendars redraw
/// live. Row types are property classes for Dapper (unsigned ids).
/// </summary>
public static class CalendarUtility
{
    public const int StaffRank = 5;
    public const int MaxTitle = 64;
    public const int MaxDescription = 500;
    public const int MaxHost = 32;
    public const string DefaultColour = "#3f8fbf";
    // events older than this fall off the calendar
    private const int PastWindowSeconds = 30 * 24 * 3600;

    private static readonly Regex ColourPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    public class EventRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public int StartsAt { get; set; }
        public int EndsAt { get; set; }
        // all-day: sits in the day's all-day row instead of the timeline
        public bool AllDay { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; } = "";
        public string Colour { get; set; } = DefaultColour;
        public string HostName { get; set; } = "";
        public string PostedBy { get; set; } = "";
    }

    public class BirthdayRow
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public int Month { get; set; }
        public int Day { get; set; }
    }

    public static bool IsStaff(Habbo habbo) => habbo != null && habbo.Rank >= StaffRank;

    public static string CleanColour(string colour) => (colour != null && ColourPattern.IsMatch(colour)) ? colour.ToLowerInvariant() : DefaultColour;

    public static List<EventRow> GetEvents()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<EventRow>(
            "SELECT e.`id` AS Id, e.`title` AS Title, e.`description` AS Description, e.`starts_at` AS StartsAt, e.`ends_at` AS EndsAt, e.`all_day` AS AllDay, " +
            "e.`room_id` AS RoomId, COALESCE(r.`caption`, '') AS RoomName, e.`colour` AS Colour, e.`host_name` AS HostName, COALESCE(u.`username`, '') AS PostedBy " +
            "FROM `rp_events` e " +
            "LEFT JOIN `rooms` r ON r.`id` = e.`room_id` " +
            "LEFT JOIN `users` u ON u.`id` = e.`created_by` " +
            "WHERE e.`ends_at` >= @since ORDER BY e.`starts_at`", new { since = (int)UnixTimestamp.GetNow() - PastWindowSeconds }).ToList();
    }

    /// <summary>The viewer's own birthday plus every friend's (both friendship directions).</summary>
    public static List<BirthdayRow> GetBirthdays(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<BirthdayRow>(
            "SELECT b.`user_id` AS UserId, u.`username` AS Username, b.`month` AS Month, b.`day` AS Day " +
            "FROM `rp_user_birthdays` b INNER JOIN `users` u ON u.`id` = b.`user_id` " +
            "WHERE b.`user_id` = @userId " +
            "OR b.`user_id` IN (SELECT `user_two_id` FROM `messenger_friendships` WHERE `user_one_id` = @userId) " +
            "OR b.`user_id` IN (SELECT `user_one_id` FROM `messenger_friendships` WHERE `user_two_id` = @userId) " +
            "ORDER BY u.`username`", new { userId }).ToList();
    }

    public static RpCalendarComposer Compose(GameClient session, List<EventRow> events)
    {
        var habbo = session.GetHabbo();
        return new RpCalendarComposer(IsStaff(habbo), events, GetBirthdays(habbo.Id));
    }

    public static void SendCalendar(GameClient session)
    {
        if (session.GetHabbo() == null) return;
        session.Send(Compose(session, GetEvents()));
    }

    /// <summary>After a staff edit: one events query, one composition per online client.</summary>
    public static void BroadcastCalendar()
    {
        var events = GetEvents();
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
        {
            if (client?.GetHabbo() == null) continue;
            client.Send(Compose(client, events));
        }
    }
}
