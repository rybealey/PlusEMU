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
/// a flashbang held, a can of pepper spray held, and a loaded stun gun in
/// hand.
///
/// Walk-up and facing are the ATM's (InteractorAtm): used from across the
/// room, the officer walks over first.
///
/// A restock says NOTHING to the room - there used to be a "*restocks their
/// ...*" bubble. The officer alone is whispered what they got: "You have
/// received a ..." for each thing handed out, and "Your stun gun has been
/// replenished." for a reload.
/// </summary>
public class InteractorPoliceReplenish : IFurniInteractor
{
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
        // Spent on the spray, like the flashbang on the throw (PepperSpray).
        var needsPepperSpray = !inventory.Any(entry => entry.Item == PepperSpray.Item);
        // The Weapon slot first: one already equipped is the one that counts.
        var stunSlot = inventory.Any(entry => entry.Slot == RpWeapons.WeaponSlot && entry.Item == RpWeapons.StunGunItem)
            ? RpWeapons.WeaponSlot
            : inventory.FirstOrDefault(entry => entry.Item == RpWeapons.StunGunItem).Slot;
        // A stun gun holds seven shots (PoliceState.StunGunShots); one that has
        // fired any is reloaded here, and a new one comes full.
        var needsReload = (stunSlot != 0) && (PoliceState.StunGunShotsLeft(habbo.Id) < PoliceState.StunGunShots);

        if (!needsCuffs && !needsFlashbang && !needsPepperSpray && stunSlot == RpWeapons.WeaponSlot && !needsReload)
        {
            session.SendWhisper("You're already fully equipped.");
            return;
        }

        // Each one on its own: a full backpack that has room for one of the two
        // still gets that one, and is told plainly which did not fit.
        // What was handed out, so the backpack update below goes out for it
        // and each gets its whisper.
        var cuffsGiven = false;
        var equipped = false;
        var flashbangGiven = false;
        var pepperSprayGiven = false;
        var stunGunGiven = false;
        // The gun they already held, refilled - not a new one.
        var stunGunReloaded = false;
        if (needsCuffs)
        {
            var slot = habbo.AddRpItem(CuffCommand.HandcuffsItem);
            if (slot == -1)
                session.SendWhisper("Your backpack is full - there was no room for the handcuffs.");
            else if (slot > 0)
                cuffsGiven = true;
        }

        if (needsFlashbang)
        {
            var slot = habbo.AddRpItem(Flashbang.Item);
            if (slot == -1)
                session.SendWhisper("Your backpack is full - there was no room for the flashbang.");
            else if (slot > 0)
                flashbangGiven = true;
        }

        if (needsPepperSpray)
        {
            var slot = habbo.AddRpItem(PepperSpray.Item);
            if (slot == -1)
                session.SendWhisper("Your backpack is full - there was no room for the pepper spray.");
            else if (slot > 0)
                pepperSprayGiven = true;
        }

        if (stunSlot == 0)
        {
            // None held. Straight into an empty Weapon slot; with another
            // weapon equipped, into the backpack first and swapped in below.
            if (habbo.AddRpItemEquipped(RpWeapons.StunGunItem))
            {
                stunGunGiven = true;
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
                    stunGunGiven = true;
                    PoliceState.RestockStunGun(habbo.Id);
                }
            }
        }
        else if (needsReload)
        {
            // The gun they hold, refilled.
            PoliceState.RestockStunGun(habbo.Id);
            stunGunReloaded = true;
        }

        // One in a carry slot, held already or just handed out: move it into
        // the Weapon slot. A swap, so an equipped knife or bat lands in the
        // slot the stun gun left and nothing needs a free space.
        if (stunSlot > 0 && stunSlot != RpWeapons.WeaponSlot)
        {
            habbo.MoveRpItem(stunSlot, RpWeapons.WeaponSlot);
            equipped = true;
        }

        if (!cuffsGiven && !equipped && !flashbangGiven && !pepperSprayGiven && !stunGunGiven && !stunGunReloaded)
            return;

        var after = habbo.LoadRpInventory();
        if (equipped)
            RpWeapons.ApplyToHand(habbo, after);
        session.Send(new RpInventoryComposer(after));
        // The stun gun's bar in the backpack, full again after a reload or a new gun.
        session.Send(new RpStunGunChargeComposer(PoliceState.StunGunShotsLeft(habbo.Id), PoliceState.StunGunShots));

        // Told privately, one line per thing, once the backpack shows it. Moving
        // a stun gun they already had into the Weapon slot says nothing.
        if (stunGunGiven)
            session.SendWhisper("You have received a stun gun.");
        if (stunGunReloaded)
            session.SendWhisper("Your stun gun has been replenished.");
        if (flashbangGiven)
            session.SendWhisper("You have received a flashbang.");
        if (pepperSprayGiven)
            session.SendWhisper("You have received a pepper spray.");
        if (cuffsGiven)
            session.SendWhisper("You have received a pair of handcuffs.");
    }
}
