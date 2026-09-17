using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: leave the jam - the button, not a dropped connection. The last one
// out ends it; a host leaving hands it to whoever has been there longest.
internal class RpJamLeaveEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        JamManager.Leave(session.GetHabbo());
        return Task.CompletedTask;
    }
}
