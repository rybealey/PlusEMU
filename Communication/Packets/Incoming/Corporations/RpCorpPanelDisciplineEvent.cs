using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Corporations;

/// <summary>
/// pixelrp Corporation Panel: issue (0) or revoke (1) a warning or strike.
/// Action, id (the employee's user id to issue, the entry's id to revoke),
/// kind ("warning" / "strike"), reason, details, and whether to alert the
/// employee. CorpDiscipline checks rank and corporation every time.
/// </summary>
internal class RpCorpPanelDisciplineEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // Two ints, three strings and a bool at the least.
        if (session?.GetHabbo() == null || packet.Buffer.Length < 2 * sizeof(int) + 3 * sizeof(short) + 1)
            return Task.CompletedTask;

        var action = packet.ReadInt();
        var id = packet.ReadInt();
        var kind = packet.ReadString();
        var reason = packet.ReadString();
        var details = packet.ReadString();
        var notify = packet.ReadBool();
        CorpPanel.HandleDiscipline(session, action, id, kind, reason, details, notify);
        return Task.CompletedTask;
    }
}
