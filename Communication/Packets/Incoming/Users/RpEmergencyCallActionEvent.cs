using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: a button in the Emergency Calls window - a call id and an action
/// (EmergencyCalls.Action*: respond, go to room, helpful, abuse). Everything is
/// checked there; the window only offers what applies.
/// </summary>
internal class RpEmergencyCallActionEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var callId = packet.ReadInt();
        var action = packet.ReadInt();
        if (session.GetHabbo() != null)
            EmergencyCalls.Act(session, callId, action);
        return Task.CompletedTask;
    }
}
