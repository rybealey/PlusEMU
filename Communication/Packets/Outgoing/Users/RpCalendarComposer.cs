using Plus.HabboHotel.Calendar;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the phone Calendar's whole state for one viewer - whether they may
/// edit (staff), every upcoming event, and the birthdays of them and their
/// friends. Sent on request and re-sent to everyone after any staff change.
/// </summary>
public class RpCalendarComposer : IServerPacket
{
    private readonly bool _canEdit;
    private readonly List<CalendarUtility.EventRow> _events;
    private readonly List<CalendarUtility.BirthdayRow> _birthdays;

    public uint MessageId => ServerPacketHeader.RpCalendarComposer;

    public RpCalendarComposer(bool canEdit, List<CalendarUtility.EventRow> events, List<CalendarUtility.BirthdayRow> birthdays)
    {
        _canEdit = canEdit;
        _events = events;
        _birthdays = birthdays;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_canEdit ? 1 : 0);
        packet.WriteInteger(_events.Count);
        foreach (var e in _events)
        {
            packet.WriteInteger(e.Id);
            packet.WriteString(e.Title ?? "");
            packet.WriteString(e.Description ?? "");
            packet.WriteInteger(e.StartsAt);
            packet.WriteInteger(e.EndsAt);
            packet.WriteInteger(e.RoomId);
            packet.WriteString(e.RoomName ?? "");
            packet.WriteString(e.Colour ?? "");
            packet.WriteString(e.HostName ?? "");
            packet.WriteString(e.PostedBy ?? "");
            packet.WriteInteger(e.AllDay ? 1 : 0);
        }
        packet.WriteInteger(_birthdays.Count);
        foreach (var b in _birthdays)
        {
            packet.WriteInteger(b.UserId);
            packet.WriteString(b.Username ?? "");
            packet.WriteInteger(b.Month);
            packet.WriteInteger(b.Day);
        }
    }
}
