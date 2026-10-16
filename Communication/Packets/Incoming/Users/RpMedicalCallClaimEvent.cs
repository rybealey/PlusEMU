using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the Claim button in Paramedic Dispatch - a call id. Everything is
/// checked in MedicalCalls.Claim; the window only offers it to the paramedic
/// the call is with.
/// </summary>
internal class RpMedicalCallClaimEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var callId = packet.ReadInt();
        if (session.GetHabbo() != null)
            MedicalCalls.Claim(session, callId);
        return Task.CompletedTask;
    }
}
