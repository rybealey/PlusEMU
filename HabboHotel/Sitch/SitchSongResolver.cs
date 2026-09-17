using System.Text.Json;
using Plus.HabboHotel.Rooms.Jukebox;

namespace Plus.HabboHotel.Sitch;

/// <summary>
/// pixelrp: a YouTube link to the three things Sitch stores about a song.
///
/// Shared by the profile's favorite song and by a song attached to a post, so
/// the two cannot drift: the same link is accepted, refused and described the
/// same way in both places.
///
/// The id comes from JukeboxStation.ParseVideoId, which already handles watch,
/// shorts, embed and youtu.be links. The metadata comes from YouTube's oEmbed
/// endpoint - the same call RpJukeboxAddEvent makes - which doubles as an
/// existence and embeddability check, so a dead or private link is refused
/// rather than saved as a card that renders nothing.
///
/// No duration: oEmbed does not return one, and the only reason the jukebox
/// knows a duration is that a client reports it back after playing. Neither a
/// profile card nor a post ever plays on its own, so neither would learn one.
/// </summary>
public static class SitchSongResolver
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public class Song
    {
        public bool Ok { get; set; }
        public string VideoId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        /// <summary>What to tell the player when Ok is false. Empty when the
        /// link was blank, which is not an error - it means "no song".</summary>
        public string Error { get; set; } = "";
    }

    /// <summary>Blank comes back Ok with an empty id: no song is not a failure.</summary>
    public static async Task<Song> Resolve(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new Song { Ok = true };

        var videoId = JukeboxStation.ParseVideoId(url);
        if (string.IsNullOrEmpty(videoId))
            return new Song { Error = "That does not look like a YouTube link." };

        try
        {
            var json = await Http.GetStringAsync(
                $"https://www.youtube.com/oembed?url=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3D{videoId}&format=json");
            using var doc = JsonDocument.Parse(json);
            return new Song
            {
                Ok = true,
                VideoId = videoId,
                Title = doc.RootElement.GetProperty("title").GetString() ?? videoId,
                Author = doc.RootElement.TryGetProperty("author_name", out var name) ? (name.GetString() ?? "") : ""
            };
        }
        catch
        {
            return new Song { Error = "That video can't be used (missing, private, or embedding disabled)." };
        }
    }
}
