using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: one move in the bank deposit box - a direction (DepositBox.Store /
/// Withdraw), the slot it comes from, and whether the whole stack goes (a
/// drag) or just one (a click). DepositBox.Move holds every rule, starting with
/// standing on a deposit box. Both sides are sent back after it.
/// </summary>
internal class RpDepositMoveEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var direction = packet.ReadInt();
        var slot = packet.ReadInt();
        var all = packet.ReadBool();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var notice = DepositBox.Move(session, direction, slot, all);
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        session.Send(new RpDepositBoxComposer(true, DepositBox.OpenSlots(habbo), DepositBox.Load(habbo.Id), notice));
        return Task.CompletedTask;
    }
}
