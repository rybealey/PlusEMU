using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: whether the session user's Discord account is linked. A bare
/// boolean by design - Discord details are never shared in-game.
/// </summary>
public class RpDiscordStatusComposer : IServerPacket
{
    private readonly bool _linked;

    public uint MessageId => ServerPacketHeader.RpDiscordStatusComposer;

    public RpDiscordStatusComposer(bool linked)
    {
        _linked = linked;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_linked ? 1 : 0);
    }
}
