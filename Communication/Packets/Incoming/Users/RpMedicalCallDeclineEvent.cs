using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the Decline button in Paramedic Dispatch - a call id. Everything is
/// checked in MedicalCalls.Decline; the window only offers it to the paramedic
/// the call is with.
/// </summary>
internal class RpMedicalCallDeclineEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var callId = packet.ReadInt();
        if (session.GetHabbo() != null)
            MedicalCalls.Decline(session, callId);
        return Task.CompletedTask;
    }
}
