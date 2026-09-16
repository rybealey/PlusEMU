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

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var body = _wordFilterManager.CheckMessage(packet.ReadString() ?? "");
        var parentId = packet.ReadInt();
        var photoId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null) return Task.CompletedTask;

        if (body.Length > SitchUtility.MaxBody) body = body.Substring(0, SitchUtility.MaxBody);
        body = body.Trim();

        // A post with neither words nor a picture is nothing at all.
        if (body.Length == 0 && photoId <= 0)
        {
            session.SendWhisper("Say something first.");
            return Task.CompletedTask;
        }

        var wait = SitchUtility.CooldownLeft(habbo.Id);
        if (wait > 0)
        {
            session.SendWhisper($"Give it {wait} more second{(wait == 1 ? "" : "s")}.");
            return Task.CompletedTask;
        }

        // Somebody else's photo is not yours to post. Checked rather than
        // trusted: the id arrives from the client.
        if (photoId > 0 && !SitchUtility.OwnsPhoto(habbo.Id, photoId))
        {
            session.SendWhisper("That photo is not in your library.");
            return Task.CompletedTask;
        }

        if (SitchUtility.ContainsLink(body))
        {
            session.SendWhisper("Links can't be posted on Sitch.");
            return Task.CompletedTask;
        }

        var id = SitchUtility.CreatePost(habbo.Id, body, parentId, photoId);
        if (id == 0)
        {
            session.SendWhisper("That post has gone.");
            return Task.CompletedTask;
        }

        // Answer with the view they are looking at, so the new post appears
        // where they wrote it rather than only after a reopen.
        if (parentId > 0) SitchUtility.SendThread(session, parentId);
        else SitchUtility.SendFeed(session, false);
        return Task.CompletedTask;
    }
}
