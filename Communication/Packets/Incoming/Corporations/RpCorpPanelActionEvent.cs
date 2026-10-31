using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Corporations;

/// <summary>
/// pixelrp Corporation Panel: one action from the window - open (0),
/// dashboard (1) or weekly shifts (2), each followed by one int (weeks ago,
/// 0-2; unused ones 0). CorpPanel.Handle checks the sender's rank every time.
/// </summary>
internal class RpCorpPanelActionEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        // Two ints or it is not the panel's: ignored, never read past its end.
        if (session?.GetHabbo() == null || packet.Buffer.Length < 2 * sizeof(int))
            return Task.CompletedTask;

        var action = packet.ReadInt();
        var arg = packet.ReadInt();
        CorpPanel.Handle(session, action, arg);
        return Task.CompletedTask;
    }
}
