using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the client toggled airplane mode from the phone's Settings app.
/// Persist the flag on the user row and echo the new state back so every open
/// client stays in sync.
/// </summary>
internal class SetAirplaneModeEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        var enabled = packet.ReadBool();
        habbo.AirplaneMode = enabled;
        habbo.SaveKey("airplane_mode", enabled ? "1" : "0");
        session.Send(new RpAirplaneModeComposer(enabled));
        return Task.CompletedTask;
    }
}
