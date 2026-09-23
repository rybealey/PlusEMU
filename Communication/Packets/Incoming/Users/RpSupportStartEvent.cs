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

        var threadId = SupportUtility.StartThread(habbo.Id, category, body, out var failure);
        if (threadId == 0)
        {
            // Say which thing went wrong. This used to whisper "you already
            // have a conversation open" for every failure including the ones
            // that were our fault, which sent players looking for a
            // conversation that was not there.
            session.SendWhisper(failure switch
            {
                SupportUtility.StartFailure.AtCap =>
                    $"You already have {SupportUtility.PlayerOpenCap} support conversations open. Close one and try again.",
                SupportUtility.StartFailure.Empty =>
                    "Tell us what happened and we'll pass it on.",
                _ => "Support could not open that conversation. Try again in a moment."
            });
            // The view goes back either way: it is what tells the app the
            // start did not take, so it can hand the player their words back
            // instead of sitting on a screen it cannot send from.
            SupportUtility.SendView(session);
            return Task.CompletedTask;
        }
        SupportUtility.SendView(session, threadId);
        SupportUtility.PushToStaff();
        return Task.CompletedTask;
    }
}
