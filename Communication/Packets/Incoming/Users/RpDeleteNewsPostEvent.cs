using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.News;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: the author (or senior staff) deletes a story; it disappears for everyone at once.</summary>
internal class RpDeleteNewsPostEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (!NewsUtility.IsStaff(habbo) || id <= 0) return Task.CompletedTask;
        var existing = NewsUtility.GetPost(id);
        if (existing == null) return Task.CompletedTask;
        if (existing.AuthorId != habbo.Id && !NewsUtility.IsSenior(habbo))
        {
            session.SendWhisper("Only the author can delete that story.");
            return Task.CompletedTask;
        }
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            connection.Execute("DELETE FROM `rp_news_posts` WHERE `id` = @id", new { id });
        NewsUtility.BroadcastNews();
        return Task.CompletedTask;
    }
}
