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
/// </summary>
public static class RpWeapons
{
    /// <summary>
    /// The Weapon frame. Clear of the carry slots (1-12) and of 0, which
    /// MoveRpItem uses as its temporary slot mid-swap. Small enough for any
    /// integer column the table might have been created with.
    /// </summary>
    public const int WeaponSlot = 101;

    /// <summary>backpack item key -> (display name, handitem shown while equipped)</summary>
    private static readonly Dictionary<string, (string Name, int HandItem)> Weapons = new()
    {
        { "baseball_bat", ("Baseball Bat", 400) },
        { "knife", ("Knife", 401) },
        { "axe", ("Axe", 402) },
        // 403 is the only gun in hh_human_item; it is drawn black, the icon yellow.
        { "stun_gun", ("Stun Gun", 403) }
    };

    public static bool IsWeapon(string item) => !string.IsNullOrEmpty(item) && Weapons.ContainsKey(item);

    public static int HandItemFor(string item) => (IsWeapon(item) ? Weapons[item].HandItem : 0);

    public static string NameOf(string item) => (IsWeapon(item) ? Weapons[item].Name : item);

    public static string EquippedItem(List<(int Slot, string Item, int Count)> inventory) =>
        inventory.FirstOrDefault(entry => entry.Slot == WeaponSlot).Item;

    /// <summary>
    /// Bring the avatar's hand in line with what is equipped. Called after
    /// anything that can change the Weapon slot, and on room entry. A player
    /// not in a room has no hand to change; room entry catches them up.
    /// </summary>
    public static void ApplyToHand(Habbo habbo, List<(int Slot, string Item, int Count)> inventory = null)
    {
        var user = habbo?.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (user == null)
            return;
        user.SetWeaponHandItem(HandItemFor(EquippedItem(inventory ?? habbo.LoadRpInventory())));
    }
}
