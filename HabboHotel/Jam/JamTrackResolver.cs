using System.Text.Json;
using Plus.HabboHotel.Rooms.Jukebox;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Jam;

/// <summary>
/// pixelrp: a YouTube id into a named track.
///
/// Server-side for the two reasons the room jukebox's is: the browser cannot
/// reach oEmbed at all (it refuses cross-origin requests), and a title supplied
/// by a client is a title that client chose - which matters here, because these
/// names are shown to other people.
///
/// Shared by the two paths that need it, adding a song to a jam and starting one
/// with a song already playing. It was one path when this was written inline in
/// the add handler; a second copy of an HTTP call and a parse is a second place
/// for the failure handling to drift.
/// </summary>
public static class JamTrackResolver
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>The named track, or null if YouTube will not own up to it -
    /// missing, private, or embedding disabled.</summary>
    public static async Task<JukeboxTrack> Resolve(string videoId, Habbo requester)
    {
        if (string.IsNullOrWhiteSpace(videoId) || requester == null)
            return null;
        try
        {
            var json = await Http.GetStringAsync(
                $"https://www.youtube.com/oembed?url=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3D{videoId}&format=json");
            using var doc = JsonDocument.Parse(json);
            return new JukeboxTrack
            {
                VideoId = videoId,
                Title = doc.RootElement.GetProperty("title").GetString() ?? videoId,
                Author = doc.RootElement.TryGetProperty("author_name", out var author) ? (author.GetString() ?? "") : "",
                DurationSec = 0,
                QueuedBy = requester.Username,
                QueuedById = requester.Id
            };
        }
        catch
        {
            return null;
        }
    }
}
