using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.News;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: any staff member pins (1) or unpins (0) a story; pinning replaces the previous pin.</summary>
internal class RpPinNewsPostEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var pinned = packet.ReadInt() == 1;
        var habbo = session.GetHabbo();
        if (!NewsUtility.IsStaff(habbo) || id <= 0) return Task.CompletedTask;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (pinned) connection.Execute("UPDATE `rp_news_posts` SET `pinned` = 0 WHERE `pinned` = 1");
            connection.Execute("UPDATE `rp_news_posts` SET `pinned` = @pinned WHERE `id` = @id", new { id, pinned = pinned ? 1 : 0 });
        }
        NewsUtility.BroadcastNews();
        return Task.CompletedTask;
    }
}
