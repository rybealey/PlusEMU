using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;
using Plus.HabboHotel.Rooms.Jukebox;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: start a jam, hosting it.
//
// IT STARTS WHERE YOU ARE. A song of your own is a purely local thing until this
// moment - nothing about it has ever reached the server - so a jam started while
// one was playing used to begin EMPTY. The host went on hearing their song, saw
// a jam, invited people, and every guest arrived to silence: as far as the jam
// was concerned nothing was playing. The next song queued went through properly
// and everybody heard it, which made the whole thing look like only the first
// song was cursed.
//
// So the client sends what it is playing and how far in it is, and the queue
// behind it. The clock is wound back to match, which means the jam joins the
// song rather than the song restarting under the host.
//
// The queue comes too, and bypasses the per-person cooldown on purpose: this is
// one action - "bring my session with me" - not somebody pasting twenty links.
//
// Titles are still resolved here rather than taken from the client. They are
// shown to other people now, which is exactly when a name the client chose stops
// being harmless.
//
// Refused if they are already in one. One at a time is the rule the whole design
// leans on - "your session" means one thing on every screen only while there
// cannot be two.
internal class RpJamStartEvent : IPacketEvent
{
    private const int MaxCarriedQueue = 20;

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var videoId = JukeboxStation.ParseVideoId(packet.ReadString());
        var elapsedSec = packet.ReadInt();
        var queued = new List<string>();
        var queueCount = packet.ReadInt();
        for (var i = 0; i < queueCount; i++)
        {
            var id = JukeboxStation.ParseVideoId(packet.ReadString());
            if (id != null && queued.Count < MaxCarriedQueue)
                queued.Add(id);
        }
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        var jam = JamManager.Start(habbo);
        if (jam == null)
        {
            session.SendNotification("You're already in a jam. Leave that one first.");
            JamManager.SendState(session);
            return;
        }
        // Sent before the lookups below, which reach YouTube and take a moment.
        // The host should see their jam the instant they ask for it, not after a
        // round trip to a third party.
        jam.BroadcastState();
        if (videoId != null)
        {
            var current = await JamTrackResolver.Resolve(videoId, habbo);
            // Re-read rather than captured: the awaits here are long enough for
            // the host to have ended the jam or started another.
            if (JamManager.GetFor(habbo.Id) != jam)
                return;
            if (current != null)
                jam.SeedCurrent(current, elapsedSec);
        }
        foreach (var id in queued)
        {
            var track = await JamTrackResolver.Resolve(id, habbo);
            if (JamManager.GetFor(habbo.Id) != jam)
                return;
            if (track != null)
                jam.Enqueue(track);
        }
    }
}
