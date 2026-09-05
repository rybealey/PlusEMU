using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.News;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: News app opened - the feed for this viewer.</summary>
internal class RpGetNewsEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null) return Task.CompletedTask;
        NewsUtility.SendNews(session);
        return Task.CompletedTask;
    }
}
