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
        var weaponSlot = Plus.HabboHotel.Users.RpWeapons.WeaponSlot;
        if ((slot < 1 || slot > Plus.HabboHotel.Users.Habbo.RpCarrySlots) && slot != weaponSlot)
            return Task.CompletedTask;
        var (item, _) = habbo.DiscardRpItem(slot, count);
        if (item == null)
            return Task.CompletedTask;
        var after = habbo.LoadRpInventory();
        // Binning the equipped weapon empties the hand with it.
        if (slot == weaponSlot)
            Plus.HabboHotel.Users.RpWeapons.ApplyToHand(habbo, after);
        session.Send(new RpInventoryComposer(after));
        return Task.CompletedTask;
    }
}
