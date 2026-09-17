using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>
/// pixelrp: write a Sitch post, or a reply when parentId is set.
///
/// The body goes through the word filter, like every other place in this hotel
/// where a player types something other people read. The 280 limit is enforced
/// HERE as well as in the composer - the client's counter is a courtesy, not a
/// control, and nothing stops a crafted packet.
///
/// Links are refused outright rather than stripped. Stripping leaves a sentence
/// that reads like it lost a word and gives no reason, and somebody who meant to
/// advertise simply posts again; a refusal says what happened once. Checked on
/// the body AFTER filtering and trimming, because that is what would actually
/// have been stored.
/// </summary>
internal class RpSitchPostEvent : IPacketEvent
{
    private readonly IWordFilterManager _wordFilterManager;

    public RpSitchPostEvent(IWordFilterManager wordFilterManager) => _wordFilterManager = wordFilterManager;

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var body = _wordFilterManager.CheckMessage(packet.ReadString() ?? "");
        var parentId = packet.ReadInt();
        var photoId = packet.ReadInt();
        // Read before any early return: the whole record has to come off the
        // wire whatever happens to it.
        var songUrl = packet.ReadString() ?? "";
        var habbo = session.GetHabbo();
        if (habbo == null) return;

        if (body.Length > SitchUtility.MaxBody) body = body.Substring(0, SitchUtility.MaxBody);
        body = body.Trim();

        // A post with no words, no picture and no song is nothing at all. A
        // song on its own is a post - "here, listen to this" is the whole
        // message.
        if (body.Length == 0 && photoId <= 0 && string.IsNullOrWhiteSpace(songUrl))
        {
            session.SendWhisper("Say something first.");
            return;
        }

        var wait = SitchUtility.CooldownLeft(habbo.Id);
        if (wait > 0)
        {
            session.SendWhisper($"Give it {wait} more second{(wait == 1 ? "" : "s")}.");
            return;
        }

        // Somebody else's photo is not yours to post. Checked rather than
        // trusted: the id arrives from the client.
        if (photoId > 0 && !SitchUtility.OwnsPhoto(habbo.Id, photoId))
        {
            session.SendWhisper("That photo is not in your library.");
            return;
        }

        // The BODY, not the song field. A song is a link by definition and has
        // its own box precisely so the rule about links in prose can stay.
        if (SitchUtility.ContainsLink(body))
        {
            session.SendWhisper("Links can't be posted on Sitch.");
            return;
        }

        // A bad link refuses the whole post rather than dropping the song
        // quietly - somebody who pasted a link meant to attach it, and a post
        // that silently lost it is worse than one that did not go out.
        var song = await SitchSongResolver.Resolve(songUrl);
        if (!song.Ok)
        {
            session.SendWhisper(song.Error);
            return;
        }

        var id = SitchUtility.CreatePost(habbo.Id, body, parentId, photoId,
            song.VideoId, song.Title, song.Author);
        if (id == 0)
        {
            session.SendWhisper("That post has gone.");
            return;
        }

        // Answer with the view they are looking at, so the new post appears
        // where they wrote it rather than only after a reopen.
        if (parentId > 0) SitchUtility.SendThread(session, parentId);
        else SitchUtility.SendFeed(session, false);
    }
}
