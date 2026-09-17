using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: start a jam, hosting it.
//
// Refused if they are already in one. One at a time is the rule the whole
// design leans on - "your session" means one thing on every screen only while
// there cannot be two.
internal class RpJamStartEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        var jam = JamManager.Start(habbo);
        if (jam == null)
        {
            session.SendNotification("You're already in a jam. Leave that one first.");
            JamManager.SendState(session);
            return Task.CompletedTask;
        }
        jam.BroadcastState();
        return Task.CompletedTask;
    }
}
