using Plus.HabboHotel.Calendar;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: the phone Calendar opened - send its state.</summary>
internal class RpGetCalendarEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        CalendarUtility.SendCalendar(session);
        return Task.CompletedTask;
    }
}
