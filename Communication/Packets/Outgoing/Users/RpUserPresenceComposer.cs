using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the answer to RpUserPresenceEvent - the name as it was asked and
/// whether that player is online; when they are, what the HUD needs to put
/// them on its target plate from another room: their id, their name as they
/// spell it, their look and their motto. Never which room they are in.
/// </summary>
public class RpUserPresenceComposer : IServerPacket
{
    private readonly string _asked;
    private readonly Habbo? _habbo;

    public uint MessageId => ServerPacketHeader.RpUserPresenceComposer;

    public RpUserPresenceComposer(string asked, Habbo? habbo)
    {
        _asked = asked;
        _habbo = habbo;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_asked);
        packet.WriteBoolean(_habbo != null);
        if (_habbo == null)
            return;
        packet.WriteInteger(_habbo.Id);
        packet.WriteString(_habbo.Username);
        packet.WriteString(_habbo.Look);
        packet.WriteString(_habbo.Motto);
    }
}
