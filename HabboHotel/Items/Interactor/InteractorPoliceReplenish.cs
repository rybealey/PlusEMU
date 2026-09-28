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
/// backpack. A stun gun holds seven shots (PoliceState.StunGunShots), and one
/// that has fired any is reloaded here. So "fully equipped" means cuffs held,
/// a flashbang held, and a loaded stun gun in hand.
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
        // A flashbang is spent on the throw, so this is the one the locker is
        // asked for most (Flashbang).
        var needsFlashbang = !inventory.Any(entry => entry.Item == Flashbang.Item);
        // The Weapon slot first: one already equipped is the one that counts.
        var stunSlot = inventory.Any(entry => entry.Slot == RpWeapons.WeaponSlot && entry.Item == RpWeapons.StunGunItem)
            ? RpWeapons.WeaponSlot
            : inventory.FirstOrDefault(entry => entry.Item == RpWeapons.StunGunItem).Slot;
        // A stun gun holds seven shots (PoliceState.StunGunShots); one that has
        // fired any is reloaded here, and a new one comes full.
        var needsReload = (stunSlot != 0) && (PoliceState.StunGunShotsLeft(habbo.Id) < PoliceState.StunGunShots);

        if (!needsCuffs && !needsFlashbang && stunSlot == RpWeapons.WeaponSlot && !needsReload)
        {
            session.SendWhisper("You're already fully equipped.");
            return;
        }

        // Each one on its own: a full backpack that has room for one of the two
        // still gets that one, and is told plainly which did not fit.
        var given = new List<string>();
        var equipped = false;
        // The flashbang is restocked QUIETLY - never named in the restock chat -
        // but it still counts as something handed out, so the backpack update
        // below goes out for it.
        var flashbangGiven = false;
        if (needsCuffs)
        {
            var slot = habbo.AddRpItem(CuffCommand.HandcuffsItem);
            if (slot == -1)
                session.SendWhisper("Your backpack is full - there was no room for the handcuffs.");
            else if (slot > 0)
                given.Add("handcuffs");
        }

        if (needsFlashbang)
        {
            var slot = habbo.AddRpItem(Flashbang.Item);
            if (slot == -1)
                session.SendWhisper("Your backpack is full - there was no room for the flashbang.");
            else if (slot > 0)
                flashbangGiven = true;
        }

        if (stunSlot == 0)
        {
            // None held. Straight into an empty Weapon slot; with another
            // weapon equipped, into the backpack first and swapped in below.
            if (habbo.AddRpItemEquipped(RpWeapons.StunGunItem))
            {
                given.Add("stun gun");
                equipped = true;
                PoliceState.RestockStunGun(habbo.Id);
            }
            else
            {
                stunSlot = habbo.AddRpItem(RpWeapons.StunGunItem);
                if (stunSlot == -1)
                    session.SendWhisper("Your backpack is full - there was no room for the stun gun.");
                else if (stunSlot > 0)
                {
                    given.Add("stun gun");
                    PoliceState.RestockStunGun(habbo.Id);
                }
            }
        }
        else if (needsReload)
        {
            // The gun they hold, refilled - named in the restock chat like a new one.
            PoliceState.RestockStunGun(habbo.Id);
            given.Add("stun gun");
        }

        // One in a carry slot, held already or just handed out: move it into
        // the Weapon slot. A swap, so an equipped knife or bat lands in the
        // slot the stun gun left and nothing needs a free space.
        if (stunSlot > 0 && stunSlot != RpWeapons.WeaponSlot)
        {
            habbo.MoveRpItem(stunSlot, RpWeapons.WeaponSlot);
            equipped = true;
        }

        if (given.Count == 0 && !equipped && !flashbangGiven)
            return;

        var after = habbo.LoadRpInventory();
        if (equipped)
            RpWeapons.ApplyToHand(habbo, after);
        // Only a restock of the cuffs or the stun gun is announced. Equipping the
        // stun gun says nothing - the gun appearing in the officer's hand is the
        // whole of it - and neither does the flashbang (flashbangGiven).
        if (given.Count > 0)
        {
            // "handcuffs", "stun gun", "handcuffs and stun gun"
            var list = given.Count <= 1
                ? string.Join("", given)
                : string.Join(", ", given.Take(given.Count - 1)) + " and " + given[^1];
            room.SendPacket(new ChatComposer(user.VirtualId, $"*restocks their {list}*", 0, ActionBubble));
        }
        session.Send(new RpInventoryComposer(after));
        // The stun gun's bar in the backpack, full again after a reload or a new gun.
        session.Send(new RpStunGunChargeComposer(PoliceState.StunGunShotsLeft(habbo.Id), PoliceState.StunGunShots));
    }
}
