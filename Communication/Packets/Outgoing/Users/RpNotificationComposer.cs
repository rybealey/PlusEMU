using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: one phone notification, pushed the moment the thing happens.
///
/// The phone shows it as a banner at the top of the screen and, unless it is
/// transient, keeps it in the Notification Center and counts it on the app's
/// badge until the player opens the thing it points at.
///
/// This carries FACTS, not sentences: which app, what happened, the name of
/// the thing and who did it. Every line the player reads is written on the
/// client, so all of the wording (including how several of the same kind
/// collapse into "3 new photos in Rooftop Nights") lives in one place.
///
/// The server does not track what has been read: the client owns that (it
/// persists its own list per account, the same way it persists the rest of
/// the phone's preferences), so there is no seen-state to keep in sync and
/// nothing to store for accounts that are not online. A player who is logged
/// out misses what happened while they were gone, like a phone switched off.
/// </summary>
public class RpNotificationComposer : IServerPacket
{
    private readonly string _app;
    private readonly string _kind;
    private readonly string _subject;
    private readonly string _actor;
    private readonly int _targetId;
    private readonly int _extra;
    private readonly bool _transient;

    public uint MessageId => ServerPacketHeader.RpNotificationComposer;

    /// <param name="app">Which app owns it: photos, contacts, calendar, notes, news.</param>
    /// <param name="kind">What happened - the client keys its wording off this.
    /// album_invite, album_photo, note_shared, note_updated, event_new,
    /// event_changed, event_cancelled, event_soon, story.</param>
    /// <param name="subject">The name of the thing: album, note title, event
    /// title, headline. Sent even when the client could look it up, so a
    /// cancelled event can still be named after it has left the calendar.</param>
    /// <param name="actor">Who caused it, or "" when nobody did.</param>
    /// <param name="targetId">The album / note / event / post the player opens
    /// to clear it. Also what the client groups repeats by.</param>
    /// <param name="extra">Kind-specific: the old start time for
    /// event_changed, so the banner can say what it moved from. 0 otherwise.</param>
    /// <param name="transient">A banner only - never badged, never kept. For
    /// things there is nothing to go back and read.</param>
    public RpNotificationComposer(string app, string kind, string subject, string actor, int targetId, int extra = 0, bool transient = false)
    {
        _app = app ?? "";
        _kind = kind ?? "";
        _subject = subject ?? "";
        _actor = actor ?? "";
        _targetId = targetId;
        _extra = extra;
        _transient = transient;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_app);
        packet.WriteString(_kind);
        packet.WriteString(_subject);
        packet.WriteString(_actor);
        packet.WriteInteger(_targetId);
        packet.WriteInteger(_extra);
        // an int, like every other flag on the phone's client-source packets
        packet.WriteInteger(_transient ? 1 : 0);
    }
}
