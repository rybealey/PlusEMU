using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users.Banking;

/// <summary>
/// pixelrp: close the ATM screen. Sent when the player is no longer at the
/// machine they opened it at (AtmSessions.Tick) - the ATM's version of
/// stepping off the deposit box. Closing is all it says.
/// </summary>
public class RpAtmCloseComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.RpAtmCloseComposer;

    public void Compose(IOutgoingPacket packet) { }
}
