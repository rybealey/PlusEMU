using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.AI.Bots;

/// <summary>
/// pixelrp: which bots in this room are Gym bots - RpTellerBotsComposer's
/// twin, for the same reason: nothing else the client receives tells one bot
/// from another. Virtual ids; sent on room entry and whenever a bot is placed
/// or picked up. It gates the menu entry only - RpGymBotActionEvent re-checks.
/// </summary>
public class RpGymBotsComposer : IServerPacket
{
    private readonly IReadOnlyList<int> _virtualIds;

    public uint MessageId => ServerPacketHeader.RpGymBotsComposer;

    public RpGymBotsComposer(IReadOnlyList<int> virtualIds)
    {
        _virtualIds = virtualIds;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_virtualIds.Count);

        foreach (var virtualId in _virtualIds)
            packet.WriteInteger(virtualId);
    }
}
