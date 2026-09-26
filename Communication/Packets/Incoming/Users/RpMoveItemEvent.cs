using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: drag-organize the RP backpack - move the item in carry slot
/// `from` into `to`, swapping when the target is occupied. Placement mirrors
/// AddRpItem's rule: an unlocked slot always accepts, a locked (lapsed) slot
/// only takes part in a swap of something it already holds.
///
/// The Weapon frame (RpWeapons.WeaponSlot) is a slot too: dragging a weapon
/// onto it equips it, dragging it back out unequips it, and it only ever holds
/// a weapon - so a swap out of it is refused unless what comes back in is one.
/// </summary>
internal class RpMoveItemEvent : IPacketEvent
{
    private static bool IsSlot(int slot) =>
        (slot >= 1 && slot <= Plus.HabboHotel.Users.Habbo.RpCarrySlots) || slot == Plus.HabboHotel.Users.RpWeapons.WeaponSlot;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var from = packet.ReadInt();
        var to = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        if (!IsSlot(from) || !IsSlot(to) || from == to)
            return Task.CompletedTask;
        var inventory = habbo.LoadRpInventory();
        var source = inventory.FirstOrDefault(entry => entry.Slot == from);
        if (string.IsNullOrEmpty(source.Item))
            return Task.CompletedTask;
        var target = inventory.FirstOrDefault(entry => entry.Slot == to);
        var targetOccupied = !string.IsNullOrEmpty(target.Item);
        var weaponSlot = Plus.HabboHotel.Users.RpWeapons.WeaponSlot;
        if (to != weaponSlot && (to > habbo.RpUnlockedSlots) && !targetOccupied)
            return Task.CompletedTask;
        // The Weapon frame holds weapons and nothing else, from either side.
        if ((to == weaponSlot && !Plus.HabboHotel.Users.RpWeapons.IsWeapon(source.Item))
            || (from == weaponSlot && targetOccupied && !Plus.HabboHotel.Users.RpWeapons.IsWeapon(target.Item)))
        {
            session.SendWhisper("Only a weapon goes in the Weapon slot.");
            return Task.CompletedTask;
        }
        // Drawing or holstering is something your hands do; cuffs stop it,
        // the same as they stop using anything in the backpack.
        if ((to == weaponSlot || from == weaponSlot) && Plus.HabboHotel.Rooms.Chat.Commands.User.Police.PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("Your hands are cuffed.");
            return Task.CompletedTask;
        }
        habbo.MoveRpItem(from, to);
        var after = habbo.LoadRpInventory();
        if (to == weaponSlot || from == weaponSlot)
            Plus.HabboHotel.Users.RpWeapons.ApplyToHand(habbo, after);
        session.Send(new RpInventoryComposer(after));
        return Task.CompletedTask;
    }
}
