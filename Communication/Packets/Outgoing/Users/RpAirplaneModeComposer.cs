using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: pushes the player's saved airplane-mode state to their client at
/// login (and echoed after a toggle). While on, incoming friend requests are
/// hidden in the phone's Contacts app and DMs to the player bounce with a
/// "not delivered" receipt.
/// </summary>
public class RpAirplaneModeComposer : IServerPacket
{
    private readonly bool _enabled;

    public uint MessageId => ServerPacketHeader.RpAirplaneModeComposer;

    public RpAirplaneModeComposer(bool enabled)
    {
        _enabled = enabled;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_enabled);
    }
}
