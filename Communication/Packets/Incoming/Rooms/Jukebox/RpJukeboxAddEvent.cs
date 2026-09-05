using System.Text.Json;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Jukebox;

namespace Plus.Communication.Packets.Incoming.Rooms.Jukebox;

// PixelRP: client queues a YouTube URL onto the hotel's station. The station
// does synchronous pre-flight checks (jukebox present, queue space, cooldown,
// parseable video id); on success we fetch oEmbed metadata server-side
// (dodges CORS and keeps clients out of the metadata trust path) and enqueue.
internal class RpJukeboxAddEvent : IPacketEvent
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var url = packet.ReadString();
        if (session.GetHabbo() == null)
            return;
        // from a room jukebox or the phone's Music app alike - one hotel station
        var error = JukeboxStation.TryAdd(session, url);
        if (error != null)
        {
            session.SendNotification(error);
            return;
        }
        var videoId = JukeboxStation.ParseVideoId(url);
        try
        {
            // oEmbed: no API key, returns title + channel.
            var json = await Http.GetStringAsync(
                $"https://www.youtube.com/oembed?url=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3D{videoId}&format=json");
            using var doc = JsonDocument.Parse(json);
            JukeboxStation.Enqueue(new JukeboxTrack
            {
                VideoId = videoId,
                Title = doc.RootElement.GetProperty("title").GetString() ?? videoId,
                Author = doc.RootElement.TryGetProperty("author_name", out var author) ? (author.GetString() ?? "") : "",
                DurationSec = 0,
                QueuedBy = session.GetHabbo().Username,
                QueuedById = session.GetHabbo().Id
            });
        }
        catch
        {
            // 404/401 from oEmbed = video missing, private or embed-restricted.
            session.SendNotification("That video can't be played (missing, private, or embedding disabled).");
        }
    }
}
