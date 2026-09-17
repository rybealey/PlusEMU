using Plus.HabboHotel.GameClients;
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
    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var url = packet.ReadString() ?? "";
        var habbo = session.GetHabbo();
        if (habbo == null) return;

        // An empty link clears the song rather than being an error - that is
        // how somebody takes it off their profile. The resolver returns Ok with
        // an empty id for exactly that case.
        var song = await SitchSongResolver.Resolve(url);
        if (!song.Ok)
        {
            session.SendNotification(song.Error);
            return;
        }

        SitchUtility.SetFavoriteSong(habbo.Id, song.VideoId, song.Title, song.Author);
        SitchUtility.SendProfile(session, habbo.Id);
    }
}
