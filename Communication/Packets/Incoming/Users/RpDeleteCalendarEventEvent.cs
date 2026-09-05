using Dapper;
using Plus.HabboHotel.Calendar;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: staff delete a calendar event; everyone's calendar is re-sent.</summary>
internal class RpDeleteCalendarEventEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        if (!CalendarUtility.IsStaff(session.GetHabbo()) || id <= 0)
            return Task.CompletedTask;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            connection.Execute("DELETE FROM `rp_events` WHERE `id` = @id", new { id });
        CalendarUtility.BroadcastCalendar();
        return Task.CompletedTask;
    }
}
