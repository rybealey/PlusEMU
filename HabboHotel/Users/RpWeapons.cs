namespace Plus.HabboHotel.Users;

/// <summary>
/// pixelrp: the backpack items that can be EQUIPPED, and what an equipped one
/// puts in the avatar's hand.
///
/// Equipping is a move into <see cref="WeaponSlot"/>, a reserved row in
/// `user_rp_inventory` beside the twelve carry slots - so it persists, swaps,
/// and bins exactly like any other slot, and nothing new is stored. The client
/// draws that row in the backpack's Weapon frame.
///
/// While one is equipped, its handitem is the hand's RESTING item
/// (RoomUser.WeaponHandItemId): what the hand goes back to whenever something
/// else is put down. The phone outranks it - opening the phone takes the hand,
/// closing it gives the weapon back - and a drink or anything else timed
/// borrows the hand and returns it when it runs out.
///
/// EXCEPT THE STUN GUN, which is tied to its backpack slot both ways
/// (RoomUser.StunGunDrawn): equipped, it is always in the hand - over the phone,
/// and nothing else is taken into that hand - and its handitem reaches a hand
/// no other way, not by :carry, a bot, wired, furni or being handed over.
/// </summary>
public static class RpWeapons
{
    /// <summary>
    /// The Weapon frame. Clear of the carry slots (1-12) and of 0, which
    /// MoveRpItem uses as its temporary slot mid-swap. Small enough for any
    /// integer column the table might have been created with.
    /// </summary>
    public const int WeaponSlot = 101;

    /// <summary>The stun gun's item key - police equipment the locker restocks.</summary>
    public const string StunGunItem = "stun_gun";

    /// <summary>
    /// The stun gun's handitem. 403 is the only gun in hh_human_item; it is
    /// drawn black, the icon yellow.
    /// </summary>
    public const int StunGunHandItem = 403;

    /// <summary>backpack item key -> (display name, handitem shown while equipped)</summary>
    private static readonly Dictionary<string, (string Name, int HandItem)> Weapons = new()
    {
        { "baseball_bat", ("Baseball Bat", 400) },
        { "knife", ("Knife", 401) },
        { "axe", ("Axe", 402) },
        { StunGunItem, ("Stun Gun", StunGunHandItem) }
    };

    /// <summary>
    /// pixelrp (Twist, 2026-10-29): what an equipped weapon adds to a landed
    /// :hit, on top of the punch (GymState.PunchDamage) at every Strength
    /// level - at 10 a fist hits 16-19, a bat 18-20, an axe 19-21 and a knife
    /// 20-24; through a vest 13-15, 14-16, 15-17 and 16-19, every weapon
    /// clear of the next (Twist, 2026-10-29). The word is how the hit's bubble
    /// names it. One axe hit in four
    /// is a critical hit, which freezes the target (HitCommand). The stun gun
    /// and the vape are no melee weapon: holding one, a :hit is a punch.
    ///
    /// backpack item key -> (word, added to the lowest, added to the highest, chance of a critical hit)
    /// </summary>
    private static readonly Dictionary<string, (string Word, int MinBonus, int MaxBonus, double CriticalChance)> MeleeWeapons = new()
    {
        { "baseball_bat", ("bat", 2, 1, 0) },
        { "knife", ("knife", 4, 5, 0) },
        { "axe", ("axe", 3, 2, 0.25) }
    };

    public static bool IsWeapon(string item) => !string.IsNullOrEmpty(item) && Weapons.ContainsKey(item);

    /// <summary>
    /// The melee weapon this player has equipped, or null for a bare fist.
    /// Read from what their hand rests on (Habbo.WeaponHandItem), so a punch
    /// never waits on the backpack - only when that is not known yet, between
    /// rooms, is the backpack read.
    /// </summary>
    public static (string Word, int MinBonus, int MaxBonus, double CriticalChance)? MeleeOf(Habbo habbo)
    {
        var handItem = habbo.WeaponHandItem >= 0 ? habbo.WeaponHandItem : HandItemFor(EquippedItem(habbo.LoadRpInventory()));
        foreach (var (item, melee) in MeleeWeapons)
        {
            if (Weapons[item].HandItem == handItem)
                return melee;
        }
        return null;
    }

    /// <summary>
    /// What the Weapon slot takes: the weapons, and the vape - in the hand only
    /// while it is equipped there (Twist, 2026-10-22). The vape is no weapon:
    /// it still stacks, and nothing that counts weapons (trades, the deposit
    /// box, the City Panel's caps) treats it as one.
    /// </summary>
    public static bool IsEquippable(string item) => IsWeapon(item) || item == RpVape.Item;

    public static int HandItemFor(string item) =>
        IsWeapon(item) ? Weapons[item].HandItem : item == RpVape.Item ? RpVape.HandItem : 0;

    public static string NameOf(string item) => (IsWeapon(item) ? Weapons[item].Name : item);

    public static string EquippedItem(List<(int Slot, string Item, int Count)> inventory) =>
        inventory.FirstOrDefault(entry => entry.Slot == WeaponSlot).Item;

    /// <summary>
    /// Put the equipped weapon back in the first free carry slot, as clicking it
    /// in the backpack does (RpUseItemEvent). False when nothing is equipped or
    /// the backpack is full. The caller re-syncs the hand (ApplyToHand) and
    /// sends the backpack.
    /// </summary>
    public static bool TryPutAway(Habbo habbo)
    {
        var inventory = habbo.LoadRpInventory();
        if (string.IsNullOrEmpty(EquippedItem(inventory)))
            return false;
        var used = inventory.Select(entry => entry.Slot).ToHashSet();
        var free = Enumerable.Range(1, habbo.RpUnlockedSlots).FirstOrDefault(candidate => !used.Contains(candidate));
        if (free == 0)
            return false;
        habbo.MoveRpItem(WeaponSlot, free);
        return true;
    }

    /// <summary>
    /// Bring the avatar's hand in line with what is equipped. Called after
    /// anything that can change the Weapon slot, and on room entry. A player
    /// not in a room has no hand to change; room entry catches them up.
    ///
    /// pixelrp (Twist, 2026-10-27): what is equipped is remembered on the
    /// Habbo too (Habbo.WeaponHandItem), so a room entered puts it in the hand
    /// at once (RoomUserManager.AddAvatarToRoom). Between rooms, with no
    /// backpack read to hand, it is marked unknown rather than read for
    /// nothing: the next entry reads it.
    /// </summary>
    public static void ApplyToHand(Habbo habbo, List<(int Slot, string Item, int Count)> inventory = null)
    {
        if (habbo == null)
            return;
        var user = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (user == null && inventory == null)
        {
            habbo.WeaponHandItem = -1;
            return;
        }
        var handItem = HandItemFor(EquippedItem(inventory ?? habbo.LoadRpInventory()));
        habbo.WeaponHandItem = handItem;
        user?.SetWeaponHandItem(handItem);
    }
}
