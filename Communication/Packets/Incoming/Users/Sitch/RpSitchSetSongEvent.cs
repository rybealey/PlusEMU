using System.Text.Json;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Jukebox;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>
/// pixelrp: the one song on your Sitch profile.
///
/// The same gesture the Tunes app already has - paste a YouTube link - with the
/// opposite destination. This NEVER reaches the hotel queue and never plays on
/// its own; it sits on a profile for people to tap.
///
/// The id comes out of JukeboxStation.ParseVideoId, which already handles
/// watch, shorts, embed and youtu.be links, and the metadata comes from the
/// same server-side oEmbed call RpJukeboxAddEvent makes - which doubles as an
/// existence check, so a dead or private link is refused rather than saved as
/// a card that renders nothing.
///
/// No duration is stored: oEmbed does not return one, and the only reason the
/// jukebox knows a duration is that a client reports it back after playing.
/// A profile card never plays, so it would never learn one.
/// </summary>
internal class RpSitchSetSongEvent : IPacketEvent
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var url = packet.ReadString() ?? "";
        var habbo = session.GetHabbo();
        if (habbo == null) return;

        // An empty link clears the song rather than being an error - that is
        // how somebody takes it off their profile.
        if (string.IsNullOrWhiteSpace(url))
        {
            SitchUtility.SetFavoriteSong(habbo.Id, "", "", "");
            SitchUtility.SendProfile(session, habbo.Id);
            return;
        }

        var videoId = JukeboxStation.ParseVideoId(url);
        if (string.IsNullOrEmpty(videoId))
        {
            session.SendNotification("That does not look like a YouTube link.");
            return;
        }

        try
        {
            var json = await Http.GetStringAsync(
                $"https://www.youtube.com/oembed?url=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3D{videoId}&format=json");
            using var doc = JsonDocument.Parse(json);
            var title = doc.RootElement.GetProperty("title").GetString() ?? videoId;
            var author = doc.RootElement.TryGetProperty("author_name", out var name) ? (name.GetString() ?? "") : "";
            SitchUtility.SetFavoriteSong(habbo.Id, videoId, title, author);
            SitchUtility.SendProfile(session, habbo.Id);
        }
        catch
        {
            session.SendNotification("That video can't be used (missing, private, or embedding disabled).");
        }
    }
}
