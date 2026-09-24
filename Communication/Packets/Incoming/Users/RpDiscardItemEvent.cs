using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the backpack bin - dragging an item onto the bin that stands in
/// for the close button while a drag is under way, then confirming how many,
/// throws that many away from the carry slot (the whole stack when the count
/// covers it). Any slot the player holds something in may be emptied, a
/// lapsed VIP slot included; an empty slot is a no-op. The answer is a fresh
/// snapshot, as a move is.
/// </summary>
internal class RpDiscardItemEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var slot = packet.ReadInt();
        // How many to throw away; a client that sends only the slot means all.
        var count = packet.HasDataRemaining() ? packet.ReadInt() : int.MaxValue;
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        if (slot < 1 || slot > Plus.HabboHotel.Users.Habbo.RpCarrySlots)
            return Task.CompletedTask;
        var (item, _) = habbo.DiscardRpItem(slot, count);
        if (item == null)
            return Task.CompletedTask;
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        return Task.CompletedTask;
    }
}
