using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: whether the session user's Discord account is linked, plus the
/// unix timestamp it was linked at (0 when unlinked). Deliberately carries
/// no Discord identity - those details are never shared in-game.
/// </summary>
public class RpDiscordStatusComposer : IServerPacket
{
    private readonly bool _linked;
    private readonly int _linkedAt;

    public uint MessageId => ServerPacketHeader.RpDiscordStatusComposer;

    public RpDiscordStatusComposer(bool linked, int linkedAt = 0)
    {
        _linked = linked;
        _linkedAt = linkedAt;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_linked ? 1 : 0);
        packet.WriteInteger(_linked ? _linkedAt : 0);
    }
}
