using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Corporations;

/// <summary>
/// pixelrp Corporation Panel: promotion and demotion requests - request (0:
/// the employee's user id), approve (1), deny (2) or withdraw (3: the
/// request's id). Action, id, direction ("promotion" / "demotion", for a
/// request), and text (the reason for a request, the note for a decision).
/// CorpRanks checks rank and corporation every time.
/// </summary>
internal class RpCorpPanelRankEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // Two ints and two strings at the least.
        if (session?.GetHabbo() == null || packet.Buffer.Length < 2 * sizeof(int) + 2 * sizeof(short))
            return Task.CompletedTask;

        var action = packet.ReadInt();
        var id = packet.ReadInt();
        var direction = packet.ReadString();
        var text = packet.ReadString();
        CorpPanel.HandleRank(session, action, id, direction, text);
        return Task.CompletedTask;
    }
}
