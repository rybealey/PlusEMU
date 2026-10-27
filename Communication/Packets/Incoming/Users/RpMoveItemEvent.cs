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
/// a weapon or a vape (RpWeapons.IsEquippable) - so a swap out of it is refused
/// unless what comes back in is one. The Armor frame (RpDurability.ArmorSlot)
/// works the same for body armour.
///
/// The client shows a move the moment it is dropped (RpInventoryView), so a
/// move refused here answers with the backpack as it really is, whisper or no
/// whisper - it used to answer a refusal with nothing, which left nothing to
/// put the client's copy right with.
/// </summary>
internal class RpMoveItemEvent : IPacketEvent
{
    private static bool IsSlot(int slot) =>
        (slot >= 1 && slot <= Plus.HabboHotel.Users.Habbo.RpCarrySlots) || slot == Plus.HabboHotel.Users.RpWeapons.WeaponSlot
        || slot == Plus.HabboHotel.Users.RpDurability.ArmorSlot;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var from = packet.ReadInt();
        var to = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        if (!IsSlot(from) || !IsSlot(to) || from == to)
            return Refuse(session);
        // pixelrp trading: the backpack is frozen while a trade is open, the
        // same as use and discard. Rearranging slots was the one move that was
        // not, which let an offered stack be shuffled out from under the swap.
        if (Plus.HabboHotel.Rooms.Trading.RpTrades.IsTrading(habbo.Id))
        {
            return Refuse(session, Plus.HabboHotel.Rooms.Trading.RpTrades.FrozenMessage);
        }
        var inventory = habbo.LoadRpInventory();
        var source = inventory.FirstOrDefault(entry => entry.Slot == from);
        if (string.IsNullOrEmpty(source.Item))
            return Refuse(session);
        var target = inventory.FirstOrDefault(entry => entry.Slot == to);
        var targetOccupied = !string.IsNullOrEmpty(target.Item);
        var weaponSlot = Plus.HabboHotel.Users.RpWeapons.WeaponSlot;
        var armorSlot = Plus.HabboHotel.Users.RpDurability.ArmorSlot;
        var armor = Plus.HabboHotel.Users.RpDurability.BodyArmorItem;
        if (to != weaponSlot && to != armorSlot && (to > habbo.RpUnlockedSlots) && !targetOccupied)
            return Refuse(session);
        // pixelrp: the Armor frame holds body armour and nothing else, from either side.
        if ((to == armorSlot && source.Item != armor) || (from == armorSlot && targetOccupied && target.Item != armor))
        {
            return Refuse(session, "Only body armor goes in the Armor slot.");
        }
        // The Weapon frame holds weapons and the vape and nothing else, from either side.
        if ((to == weaponSlot && !Plus.HabboHotel.Users.RpWeapons.IsEquippable(source.Item))
            || (from == weaponSlot && targetOccupied && !Plus.HabboHotel.Users.RpWeapons.IsEquippable(target.Item)))
        {
            return Refuse(session, "Only a weapon or a vape goes in the Weapon slot.");
        }
        // Drawing or holstering is something your hands do; cuffs stop it,
        // the same as they stop using anything in the backpack.
        if ((to == weaponSlot || from == weaponSlot || to == armorSlot || from == armorSlot) && Plus.HabboHotel.Rooms.Chat.Commands.User.Police.PoliceState.IsCuffed(habbo.Id))
        {
            return Refuse(session, "Your hands are cuffed.");
        }
        // pixelrp (Twist, 2026-10-27): out cold or in a cell, nothing is drawn
        // or put on - the same as using it (RpUseItemEvent), which a drag got
        // round. Anything ending up in the Weapon or Armor frame counts, a swap
        // out of one included; taking a thing off stays allowed.
        var equipping = to == weaponSlot || to == armorSlot || ((from == weaponSlot || from == armorSlot) && targetOccupied);
        if (equipping && Plus.HabboHotel.Rooms.KnockedOut.Is(session))
        {
            return Refuse(session, Plus.HabboHotel.Rooms.KnockedOut.Refusal);
        }
        if (equipping && Plus.HabboHotel.Rooms.Chat.Commands.User.Police.JailState.IsJailed(habbo.Id))
        {
            return Refuse(session, "You can't do that in jail.");
        }
        // pixelrp police: the stun gun is drawn only by a police officer, on
        // duty or off - dragged onto the Weapon slot, or swapped in by dragging
        // whatever is there onto it. Only firing it needs the shift. Putting
        // one away is anyone's business.
        var drawing = (to == weaponSlot) ? source.Item : ((from == weaponSlot && targetOccupied) ? target.Item : null);
        if (drawing == Plus.HabboHotel.Users.RpWeapons.StunGunItem
            && !Plus.HabboHotel.Corporations.PoliceUtility.MayDrawStunGun(habbo.Id))
        {
            return Refuse(session, "Only police officers can draw a stun gun.");
        }
        habbo.MoveRpItem(from, to);
        var after = habbo.LoadRpInventory();
        if (to == weaponSlot || from == weaponSlot)
            Plus.HabboHotel.Users.RpWeapons.ApplyToHand(habbo, after);
        // armour put on or taken off: the vest with it
        if (to == armorSlot || from == armorSlot)
            Plus.HabboHotel.Users.RpDurability.SyncVest(habbo, after);
        session.Send(new RpInventoryComposer(after));
        return Task.CompletedTask;
    }

    private static Task Refuse(GameClient session, string whisper = null)
    {
        if (whisper != null)
            session.SendWhisper(whisper);
        session.Send(new RpInventoryComposer(session.GetHabbo().LoadRpInventory()));
        return Task.CompletedTask;
    }
}
