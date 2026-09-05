using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.News;
using Plus.HabboHotel.Rooms.Chat.Filter;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: staff create (id 0) or edit a story. Editing is the author's, or
/// senior staff's. Pinning here unpins whatever was pinned before - one top
/// story at a time. The feed goes to everyone afterwards.
/// </summary>
internal class RpSaveNewsPostEvent : IPacketEvent
{
    private readonly IWordFilterManager _wordFilterManager;

    public RpSaveNewsPostEvent(IWordFilterManager wordFilterManager)
    {
        _wordFilterManager = wordFilterManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var category = NewsUtility.CleanCategory(packet.ReadString());
        var title = _wordFilterManager.CheckMessage(packet.ReadString()).Trim();
        var body = _wordFilterManager.CheckMessage(packet.ReadString()).Trim();
        var image = NewsUtility.CleanImage(packet.ReadString());
        var pinned = packet.ReadInt() == 1;
        var habbo = session.GetHabbo();
        if (!NewsUtility.IsStaff(habbo)) return Task.CompletedTask;
        if (title.Length == 0 || body.Length == 0)
        {
            session.SendWhisper("A story needs a headline and a body.");
            return Task.CompletedTask;
        }
        if (title.Length > NewsUtility.MaxTitle) title = title.Substring(0, NewsUtility.MaxTitle);
        if (body.Length > NewsUtility.MaxBody) body = body.Substring(0, NewsUtility.MaxBody);
        var now = NewsUtility.Now();

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (id > 0)
            {
                var existing = NewsUtility.GetPost(id);
                if (existing == null) return Task.CompletedTask;
                if (existing.AuthorId != habbo.Id && !NewsUtility.IsSenior(habbo))
                {
                    session.SendWhisper("Only the author can edit that story.");
                    return Task.CompletedTask;
                }
                if (pinned) connection.Execute("UPDATE `rp_news_posts` SET `pinned` = 0 WHERE `pinned` = 1 AND `id` <> @id", new { id });
                connection.Execute(
                    "UPDATE `rp_news_posts` SET `category` = @category, `title` = @title, `body` = @body, `image` = @image, `pinned` = @pinned, `updated_at` = @now WHERE `id` = @id",
                    new { id, category, title, body, image, pinned = pinned ? 1 : 0, now });
            }
            else
            {
                if (pinned) connection.Execute("UPDATE `rp_news_posts` SET `pinned` = 0 WHERE `pinned` = 1");
                connection.Execute(
                    "INSERT INTO `rp_news_posts` (`author_id`, `category`, `title`, `body`, `image`, `pinned`, `created_at`, `updated_at`) VALUES (@userId, @category, @title, @body, @image, @pinned, @now, @now)",
                    new { userId = habbo.Id, category, title, body, image, pinned = pinned ? 1 : 0, now });
            }
        }

        NewsUtility.BroadcastNews();
        return Task.CompletedTask;
    }
}
