using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.AI.Bots;

/// <summary>
/// pixelrp: which bots in this room are bank tellers.
///
/// The client is told nothing about a bot's AI type - UsersComposer sends a
/// name, a figure and a hardcoded skills list, and nothing that distinguishes
/// a teller from any other bot standing behind a desk. Without this the teller
/// menu would have to be offered on every bot in the hotel and refused by the
/// server on most of them.
///
/// Virtual ids, not bot ids, because the virtual id is what the client has in
/// hand when somebody clicks an avatar.
///
/// Sent on room entry and again whenever a bot is placed or picked up, so the
/// set cannot go stale while somebody is standing there. It gates the
/// AFFORDANCE only - RpTellerActionEvent re-checks that the bot is really a
/// teller, the same split the room-rights push uses.
/// </summary>
public class RpTellerBotsComposer : IServerPacket
{
    private readonly IReadOnlyList<int> _virtualIds;

    public uint MessageId => ServerPacketHeader.RpTellerBotsComposer;

    public RpTellerBotsComposer(IReadOnlyList<int> virtualIds)
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
