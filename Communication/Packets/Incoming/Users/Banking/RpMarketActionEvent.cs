using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Incoming.Users.Banking;

/// <summary>
/// pixelrp: one action from the Marketplace window - refresh (0), list (1:
/// carry slot, quantity, price each), buy (2: listing id) or take back (3:
/// listing id). Three ints follow the action whatever it is, unused ones 0.
/// RpMarket answers every one with the whole market again.
/// </summary>
internal class RpMarketActionEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session?.GetHabbo() == null)
            return Task.CompletedTask;

        var action = packet.ReadInt();
        var a = packet.ReadInt();
        var b = packet.ReadInt();
        var c = packet.ReadInt();
        RpMarket.Handle(session, action, a, b, c);
        return Task.CompletedTask;
    }
}
