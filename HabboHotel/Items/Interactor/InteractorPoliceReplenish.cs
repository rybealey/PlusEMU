using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// pixelrp: the police equipment locker.
///
/// An on-duty officer double-clicks it and is TOPPED UP: a pair of handcuffs
/// if they carry none, a stun gun if they carry none. What they already have
/// is left alone, so a second click says they are fully equipped and hands
/// out nothing - which is also why there is no cooldown. It only ever fills a
/// gap, so there is nothing to farm.
///
/// The stun gun counts wherever it is, and the locker also EQUIPS it: a new
/// one goes straight into the Weapon slot, and one already in the backpack
/// is moved there. Whatever weapon was equipped before swaps back into the
/// backpack. So "fully equipped" means cuffs held and the stun gun in hand.
///
/// Walk-up and facing are the ATM's (InteractorAtm): used from across the
/// room, the officer walks over first.
/// </summary>
public class InteractorPoliceReplenish : IFurniInteractor
{
    /// <summary>The police / fight bubble, the one :cuff and :stun use.</summary>
    private const int ActionBubble = 4;

    public void OnTrigger(GameClient session, Item item, int request, bool hasRights)
    {
        if (session == null || item == null)
            return;
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        var room = item.GetRoom();
        if (room == null)
            return;
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (user == null)
            return;

        if (!Gamemap.TilesTouching(user.X, user.Y, item.GetX, item.GetY))
        {
            user.MoveTo(item.SquareInFront);
            return;
        }

        user.SetRot(Rotation.Calculate(user.X, user.Y, item.GetX, item.GetY), false);

        if (PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("Your hands are cuffed.");
            return;
        }

        if (!PoliceUtility.RequireOnDuty(session, "restock police equipment"))
            return;

        var inventory = habbo.LoadRpInventory();
        var needsCuffs = !inventory.Any(entry => entry.Item == CuffCommand.HandcuffsItem);
        // The Weapon slot first: one already equipped is the one that counts.
        var stunSlot = inventory.Any(entry => entry.Slot == RpWeapons.WeaponSlot && entry.Item == RpWeapons.StunGunItem)
            ? RpWeapons.WeaponSlot
            : inventory.FirstOrDefault(entry => entry.Item == RpWeapons.StunGunItem).Slot;

        if (!needsCuffs && stunSlot == RpWeapons.WeaponSlot)
        {
            session.SendWhisper("You're already fully equipped.");
            return;
        }

        // Each one on its own: a full backpack that has room for one of the two
        // still gets that one, and is told plainly which did not fit.
        var given = new List<string>();
        var equipped = false;
        if (needsCuffs)
        {
            var slot = habbo.AddRpItem(CuffCommand.HandcuffsItem);
            if (slot == -1)
                session.SendWhisper("Your backpack is full - there was no room for the handcuffs.");
            else if (slot > 0)
                given.Add("handcuffs");
        }

        if (stunSlot == 0)
        {
            // None held. Straight into an empty Weapon slot; with another
            // weapon equipped, into the backpack first and swapped in below.
            if (habbo.AddRpItemEquipped(RpWeapons.StunGunItem))
            {
                given.Add("stun gun");
                equipped = true;
            }
            else
            {
                stunSlot = habbo.AddRpItem(RpWeapons.StunGunItem);
                if (stunSlot == -1)
                    session.SendWhisper("Your backpack is full - there was no room for the stun gun.");
                else if (stunSlot > 0)
                    given.Add("stun gun");
            }
        }

        // One in a carry slot, held already or just handed out: move it into
        // the Weapon slot. A swap, so an equipped knife or bat lands in the
        // slot the stun gun left and nothing needs a free space.
        if (stunSlot > 0 && stunSlot != RpWeapons.WeaponSlot)
        {
            habbo.MoveRpItem(stunSlot, RpWeapons.WeaponSlot);
            equipped = true;
        }

        if (given.Count == 0 && !equipped)
            return;

        var after = habbo.LoadRpInventory();
        if (equipped)
            RpWeapons.ApplyToHand(habbo, after);
        var action = given.Count > 0
            ? $"*restocks their {string.Join(" and ", given)}*"
            : "*equips their stun gun*";
        room.SendPacket(new ChatComposer(user.VirtualId, action, 0, ActionBubble));
        session.Send(new RpInventoryComposer(after));
    }
}
