using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Support;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the staff actions on a thread - take it (0) or close it (1).
///
/// One packet with a discriminator rather than one per verb: they share every
/// guard, and a third verb later is a case rather than another header.
/// </summary>
internal class RpSupportStaffEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var action = packet.ReadInt();
        var threadId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || !SupportUtility.IsStaff(habbo) || threadId <= 0)
            return Task.CompletedTask;

        var thread = SupportUtility.Thread(threadId);
        if (thread == null)
            return Task.CompletedTask;

        switch (action)
        {
            case 0:
                SupportUtility.Claim(threadId, habbo.Id);
                break;
            case 1:
                SupportUtility.Resolve(threadId, habbo.Id);
                break;
            default:
                return Task.CompletedTask;
        }

        SupportUtility.SendView(session, action == 0 ? threadId : 0);
        SupportUtility.PushToStaff();
        // The player sees a thread close, and nothing at all when it is taken.
        SupportUtility.PushToPlayer(thread.PlayerId);
        return Task.CompletedTask;
    }
}
