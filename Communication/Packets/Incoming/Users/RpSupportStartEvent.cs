using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Support;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: a player opens a conversation. It joins the one queue; the
/// rotation offers it from the next game-loop beat.
/// </summary>
internal class RpSupportStartEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var category = packet.ReadString();
        var body = packet.ReadString();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var threadId = SupportUtility.StartThread(habbo.Id, category, body);
        if (threadId == 0)
        {
            // The only reason to refuse: they already have as many open as
            // they are allowed. Say so rather than failing silently.
            session.SendWhisper("You already have a support conversation open.");
            SupportUtility.SendView(session);
            return Task.CompletedTask;
        }
        SupportUtility.SendView(session, threadId);
        SupportUtility.PushToStaff();
        return Task.CompletedTask;
    }
}
