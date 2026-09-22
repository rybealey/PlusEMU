using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Support;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: a message in an existing conversation, from either side.
///
/// A staff member replying to a thread nobody has taken TAKES it in the same
/// breath - answering is the claim. Otherwise the first reply would race the
/// offer that is still running against somebody else.
/// </summary>
internal class RpSupportSendEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var threadId = packet.ReadInt();
        var body = packet.ReadString();
        var habbo = session.GetHabbo();
        if (habbo == null || threadId <= 0)
            return Task.CompletedTask;

        var thread = SupportUtility.Thread(threadId);
        if (thread == null || thread.Status == "resolved")
            return Task.CompletedTask;

        var staff = SupportUtility.IsStaff(habbo);
        // A player may only write in their own thread. Staff may write in any,
        // which is what makes a hand-over possible at all.
        if (!staff && thread.PlayerId != habbo.Id)
            return Task.CompletedTask;

        if (staff && thread.Status != "open")
            SupportUtility.Claim(threadId, habbo.Id);

        SupportUtility.AddMessage(threadId, habbo.Id, staff, body);
        SupportUtility.SendView(session, threadId);
        if (staff)
        {
            SupportUtility.PushToPlayer(thread.PlayerId);
            SupportUtility.PushToStaff();
        }
        else
        {
            SupportUtility.PushToStaff();
        }
        return Task.CompletedTask;
    }
}
