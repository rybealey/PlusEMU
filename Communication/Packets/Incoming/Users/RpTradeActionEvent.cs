using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Trading;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: a press in the trade window - an action (RpTrades.Action*), the
/// backpack slot and whether the whole stack (offering), and the item (taking
/// one back). Everything is checked in RpTrades.
/// </summary>
internal class RpTradeActionEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var action = packet.ReadInt();
        var slot = packet.ReadInt();
        var all = packet.ReadBool();
        var item = packet.ReadString();
        if (session.GetHabbo() != null)
            RpTrades.Act(session, action, slot, all, item);
        return Task.CompletedTask;
    }
}
