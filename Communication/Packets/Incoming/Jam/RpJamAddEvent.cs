using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;
using Plus.HabboHotel.Rooms.Jukebox;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: queue a YouTube link onto the jam you are in. Guests may, and their
// name rides along with the song - the queue list names who asked for each one,
// which is most of what makes a shared queue feel shared.
//
// The metadata is fetched server-side for the same two reasons the room's is:
// the browser cannot reach oEmbed (it refuses cross-origin requests), and a
// title the client supplied is a title the client chose.
//
// The jam is re-read after the await rather than captured: a player can leave,
// or the jam end, while YouTube is being asked, and the song must not land in a
// session they are no longer part of.
internal class RpJamAddEvent : IPacketEvent
{
    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var url = packet.ReadString();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        var jam = JamManager.GetFor(habbo.Id);
        if (jam == null)
        {
            session.SendNotification("You're not in a jam right now.");
            return;
        }
        var error = jam.TryAdd(session, url);
        if (error != null)
        {
            session.SendNotification(error);
            return;
        }
        var track = await JamTrackResolver.Resolve(JukeboxStation.ParseVideoId(url), habbo);
        if (track == null)
        {
            // 404/401 from oEmbed = video missing, private or embed-restricted.
            session.SendNotification("That video can't be played (missing, private, or embedding disabled).");
            return;
        }
        var stillHere = JamManager.GetFor(habbo.Id);
        if (stillHere == null || stillHere != jam)
            return;
        stillHere.Enqueue(track);
    }
}
