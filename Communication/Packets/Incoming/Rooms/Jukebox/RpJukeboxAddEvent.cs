using System.Text.Json;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Jukebox;

namespace Plus.Communication.Packets.Incoming.Rooms.Jukebox;

// PixelRP: client queues a YouTube URL onto THE ROOM THEY ARE IN. Which room
// that is decides which queue the song joins - a station belongs to a room, so
// the player's current room is the whole of the routing.
//
// The station does synchronous pre-flight checks (queue space, cooldown,
// parseable video id); on success we fetch oEmbed metadata server-side (dodges
// CORS and keeps clients out of the metadata trust path) and enqueue.
//
// The metadata fetch is awaited, so the room is re-read afterwards rather than
// captured: a player can walk out, or the room unload, while YouTube is being
// asked, and the song must not land in a room they have left.
//
// A song that joins the queue is announced in the room as a blue action
// bubble (style 4, the fighting system's): "*requests <title> by <artist> on
// the jukebox*", which the client shows with the requester's name in front.
internal class RpJukeboxAddEvent : IPacketEvent
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    // Long YouTube titles ("... (Official Music Video) [4K]") would fill the
    // bubble; past this the title is cut with an ellipsis.
    private const int MaxBubbleTitle = 40;

    // A YouTube channel name as an artist: auto-generated "Artist - Topic"
    // channels lose the suffix, and "ArtistVEVO" loses the VEVO.
    private static string ArtistOf(string author)
    {
        var name = (author ?? "").Trim();
        if (name.EndsWith(" - Topic", StringComparison.OrdinalIgnoreCase))
            name = name[..^" - Topic".Length].Trim();
        if (name.Length > 4 && name.EndsWith("VEVO", StringComparison.Ordinal))
            name = name[..^4].Trim();
        return name;
    }

    private static string Shorten(string text, int max)
    {
        text = (text ?? "").Trim();
        return text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var url = packet.ReadString();
        if (session.GetHabbo() == null)
            return;
        if (Plus.HabboHotel.Rooms.KnockedOut.Refuse(session))
            return;
        var room = session.GetHabbo().CurrentRoom;
        var jukebox = room?.GetJukeboxManager();
        if (jukebox == null || !jukebox.HasJukebox())
        {
            session.SendNotification("There's no jukebox in this room.");
            return;
        }
        var error = jukebox.TryAdd(session, url);
        if (error != null)
        {
            session.SendNotification(error);
            return;
        }
        var videoId = JukeboxStation.ParseVideoId(url);
        JukeboxTrack track;
        try
        {
            // oEmbed: no API key, returns title + channel.
            var json = await Http.GetStringAsync(
                $"https://www.youtube.com/oembed?url=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3D{videoId}&format=json");
            using var doc = JsonDocument.Parse(json);
            // re-read: the await above is long enough to leave the room in
            var stillHere = session.GetHabbo()?.CurrentRoom?.GetJukeboxManager();
            if (stillHere == null || stillHere != jukebox)
                return;
            track = new JukeboxTrack
            {
                VideoId = videoId,
                Title = doc.RootElement.GetProperty("title").GetString() ?? videoId,
                Author = doc.RootElement.TryGetProperty("author_name", out var author) ? (author.GetString() ?? "") : "",
                DurationSec = 0,
                QueuedBy = session.GetHabbo().Username,
                QueuedById = session.GetHabbo().Id
            };
            if (!stillHere.Enqueue(track))
                return;
        }
        catch
        {
            // 404/401 from oEmbed = video missing, private or embed-restricted.
            session.SendNotification("That video can't be played (missing, private, or embedding disabled).");
            return;
        }

        // outside the try: a hiccup announcing the song must not be reported
        // as the video failing to play
        var artist = ArtistOf(track.Author);
        var bubble = artist.Length > 0
            ? $"*requests {Shorten(track.Title, MaxBubbleTitle)} by {artist} on the jukebox*"
            : $"*requests {Shorten(track.Title, MaxBubbleTitle)} on the jukebox*";
        var habbo = session.GetHabbo();
        habbo?.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)?.OnChat(4, bubble, true);
    }
}
