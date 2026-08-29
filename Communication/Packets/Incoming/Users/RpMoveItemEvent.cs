using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: drag-organize the RP backpack - move the item in carry slot
/// `from` into `to`, swapping when the target is occupied. Placement mirrors
/// AddRpItem's rule: an unlocked slot always accepts, a locked (lapsed) slot
/// only takes part in a swap of something it already holds.
/// </summary>
internal class RpMoveItemEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var from = packet.ReadInt();
        var to = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        if (from < 1 || to < 1 || from > Plus.HabboHotel.Users.Habbo.RpCarrySlots || to > Plus.HabboHotel.Users.Habbo.RpCarrySlots || from == to)
            return Task.CompletedTask;
        var inventory = habbo.LoadRpInventory();
        var source = inventory.FirstOrDefault(entry => entry.Slot == from);
        if (string.IsNullOrEmpty(source.Item))
            return Task.CompletedTask;
        var targetOccupied = !string.IsNullOrEmpty(inventory.FirstOrDefault(entry => entry.Slot == to).Item);
        if ((to > habbo.RpUnlockedSlots) && !targetOccupied)
            return Task.CompletedTask;
        habbo.MoveRpItem(from, to);
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        return Task.CompletedTask;
    }
}
