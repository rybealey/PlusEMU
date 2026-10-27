using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Users;

/// <summary>
/// pixelrp: weapons and body armour wear out (Twist, 2026-10-25).
///
/// A bat, knife or axe wears by the damage its hits deal (:hit, while it is
/// equipped), body armour by the damage its wearer takes (:hit, :slap and
/// :spit, while it is worn). Each is good for <see cref="MaxWear"/> - 1200 -
/// and breaks on the hit that reaches it: it is gone from the backpack and its
/// owner is told. While it holds, armour soaks <see cref="ArmorSoak"/> - a
/// fifth - of every one of those hits, rounded to the nearest point, so the
/// wearer loses the rest; a 1-point slap is soaked by nothing but still wears
/// it by 1.
///
/// The wear lives on the item's own backpack row (user_rp_inventory.wear,
/// migration 310). Weapons and armour never stack, one to a row, so the wear
/// goes wherever the row goes: between slots, into the deposit box and back,
/// and across a trade. The backpack shows what is left as a bar on the item
/// and in its hover bubble (RpInventoryComposer sends it).
///
/// Body armour is worn in the backpack's Armor frame (<see cref="ArmorSlot"/>),
/// a reserved row like the Weapon frame, and only armour goes there. While it
/// is worn the avatar wears a vest over their clothes (<see cref="VestPart"/>,
/// <see cref="SyncVest"/>).
/// </summary>
public static class RpDurability
{
    public const string BodyArmorItem = "body_armor";

    /// <summary>The Armor frame, beside the Weapon frame (RpWeapons.WeaponSlot).</summary>
    public const int ArmorSlot = 102;

    /// <summary>How much of each hit worn armour soaks.</summary>
    public const double ArmorSoak = 0.2;

    /// <summary>
    /// The vest an avatar wears while body armour is worn (Twist, 2026-10-25):
    /// jacket_U_kevlar, figure set 3420 - its set type and id; the colour is
    /// the wearer's (<see cref="VestFor"/>).
    /// </summary>
    public const string VestPart = "cc-3420";

    /// <summary>
    /// The vest's colour outside a gang: its palette's own grey. In a gang it
    /// takes the gang's primary colour (Twist, 2026-10-25), matched to the
    /// nearest colour of the jacket palette (<see cref="VestPalette"/>).
    /// </summary>
    private const int VestDefaultColour = 1408;

    /// <summary>The figure palette jackets (cc) are coloured from.</summary>
    private const int VestPalette = 3;

    /// <summary>A gang colour's nearest palette colour, worked out once per colour.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> PaletteMatches = new();

    /// <summary>The vest this player wears with their armour, in their gang's colour.</summary>
    public static string VestFor(Habbo habbo) => $"{VestPart}-{(habbo.VestColour > 0 ? habbo.VestColour : VestDefaultColour)}";

    /// <summary>
    /// A gang changed for this player - joined, left, recoloured, disbanded
    /// (GangUtility.BroadcastGangMembership): a vest they are wearing takes
    /// the new colour, or its grey again.
    /// </summary>
    public static void OnGangChanged(int userId, int? gangColour)
    {
        var habbo = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.GetHabbo();
        if (habbo == null || !habbo.WearingArmor)
            return;
        habbo.VestColour = gangColour.HasValue ? PaletteColourFor(gangColour.Value) : 0;
        Corporations.UniformManager.Refresh(habbo.Client);
    }

    /// <summary>
    /// The jacket palette's colour nearest a gang's RGB colour, compared the
    /// way the eye sees colour (CIELAB) with a change of hue counted double -
    /// so a grey stays grey and a blue stays blue rather than landing on a
    /// green that happens to be close in plain RGB. Every palette colour
    /// counts, not only the ones the clothes editor offers: those alone hold
    /// no true greys. The grey when the palette cannot be read.
    /// </summary>
    private static int PaletteColourFor(int rgb) => PaletteMatches.GetOrAdd(rgb & 0xFFFFFF, colour =>
    {
        if (PlusEnvironment.FigureManager == null || !PlusEnvironment.FigureManager.TryGetPalette(VestPalette, out var palette))
            return VestDefaultColour;
        var target = Lab(colour);
        var best = VestDefaultColour;
        var bestDistance = double.MaxValue;
        foreach (var candidate in palette.Colors.Values)
        {
            if (!int.TryParse(candidate.Value, System.Globalization.NumberStyles.HexNumber, null, out var hex))
                continue;
            var distance = LabDistance(target, Lab(hex));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate.Id;
            }
        }
        return best;
    });

    /// <summary>An RGB colour in CIELAB (D65 white).</summary>
    private static (double L, double A, double B) Lab(int rgb)
    {
        static double Linear(int channel)
        {
            var c = channel / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : (7.787 * t) + (16.0 / 116.0);
        var (r, g, b) = (Linear((rgb >> 16) & 0xFF), Linear((rgb >> 8) & 0xFF), Linear(rgb & 0xFF));
        var fx = F(((0.4124 * r) + (0.3576 * g) + (0.1805 * b)) / 0.95047);
        var fy = F((0.2126 * r) + (0.7152 * g) + (0.0722 * b));
        var fz = F(((0.0193 * r) + (0.1192 * g) + (0.9505 * b)) / 1.08883);
        return ((116 * fy) - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    /// <summary>
    /// How far apart two CIELAB colours look (squared): lightness and
    /// colourfulness as they are, a change of hue weighed twice over.
    /// </summary>
    private static double LabDistance((double L, double A, double B) x, (double L, double A, double B) y)
    {
        var dL = x.L - y.L;
        var dC = Math.Sqrt((x.A * x.A) + (x.B * x.B)) - Math.Sqrt((y.A * y.A) + (y.B * y.B));
        var dA = x.A - y.A;
        var dB = x.B - y.B;
        var dH = Math.Max(0, (dA * dA) + (dB * dB) - (dC * dC));
        return (dL * dL) + (dC * dC) + (4 * dH);
    }

    /// <summary>The damage each item is good for before it breaks.</summary>
    private static readonly Dictionary<string, int> MaxWear = new()
    {
        { "baseball_bat", 1200 },
        { "knife", 1200 },
        { "axe", 1200 },
        { BodyArmorItem, 1200 }
    };

    /// <summary>
    /// One wear change at a time per player, so two hits landing together
    /// cannot both break the same item. pixelrp (Twist, 2026-10-27): a lock per
    /// player (by id, of a fixed few), not one for the hotel - every hit's
    /// lookup used to queue behind every other hit's, whoever was fighting.
    /// </summary>
    private static readonly object[] Gates = Enumerable.Range(0, 64).Select(_ => new object()).ToArray();

    private static object GateFor(int userId) => Gates[(userId & int.MaxValue) % Gates.Length];

    public static bool IsDurable(string item) => !string.IsNullOrEmpty(item) && MaxWear.ContainsKey(item);

    public static int MaxOf(string item) => IsDurable(item) ? MaxWear[item] : 0;

    /// <summary>Weapons and armour: one to a slot, never a stack.</summary>
    public static bool NeverStacks(string item) => RpWeapons.IsWeapon(item) || item == BodyArmorItem;

    public static string NameOf(string item) => item == BodyArmorItem ? "Body Armor" : RpWeapons.NameOf(item);

    /// <summary>
    /// The damage a hit on <paramref name="target"/> takes off their health once
    /// their armour has soaked its share - all of it when they wear none. Wears
    /// the armour by the whole hit, and breaks it when that reaches its limit.
    /// </summary>
    public static int Absorb(Habbo target, int damage)
    {
        if (target == null || damage <= 0)
            return damage;
        int soaked;
        bool broke;
        lock (GateFor(target.Id))
        {
            var (item, wear) = Worn(target.Id, ArmorSlot);
            if (item != BodyArmorItem)
                return damage;
            soaked = (int)Math.Round(damage * ArmorSoak, MidpointRounding.AwayFromZero);
            broke = Wear(target.Id, ArmorSlot, item, wear + damage);
        }
        Tell(target, broke ? BodyArmorItem : null, weapon: false);
        return damage - soaked;
    }

    /// <summary>
    /// A hit by <paramref name="attacker"/> landed for <paramref name="damage"/>:
    /// the weapon they have equipped, if it wears, wears by it, and breaks when
    /// that reaches its limit. A stun gun or a vape in the Weapon frame does not wear.
    /// </summary>
    public static void WearWeapon(Habbo attacker, int damage)
    {
        if (attacker == null || damage <= 0)
            return;
        string item;
        bool broke;
        lock (GateFor(attacker.Id))
        {
            int wear;
            (item, wear) = Worn(attacker.Id, RpWeapons.WeaponSlot);
            if (!IsDurable(item))
                return;
            broke = Wear(attacker.Id, RpWeapons.WeaponSlot, item, wear + damage);
        }
        Tell(attacker, broke ? item : null, weapon: true);
    }

    /// <summary>
    /// Read off the backpack whether the player wears body armour, and dress
    /// them to match: the vest goes on over their own clothes (or a uniform)
    /// while it is worn, and their own jacket comes back when it is not -
    /// in memory only, so their saved look is never touched
    /// (UniformManager.Dress). After anything that changes the Armor slot, and
    /// at login.
    /// </summary>
    public static void SyncVest(Habbo habbo, List<(int Slot, string Item, int Count)>? inventory = null)
    {
        if (habbo == null)
            return;
        habbo.WearingArmor = (inventory ?? habbo.LoadRpInventory()).Any(entry => entry.Slot == ArmorSlot && entry.Item == BodyArmorItem);
        // in their gang's colour (OnGangChanged keeps it up to date after)
        if (habbo.WearingArmor)
        {
            var gang = Gangs.GangUtility.GetGang(habbo.Id);
            habbo.VestColour = gang != null ? PaletteColourFor(gang.Colour1) : 0;
        }
        if (habbo.Client != null)
            Corporations.UniformManager.Refresh(habbo.Client);
        else
            Corporations.UniformManager.Dress(habbo);
    }

    /// <summary>What sits in one of the player's slots, and its wear.</summary>
    private static (string Item, int Wear) Worn(int userId, int slot)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `item`, `wear` FROM `user_rp_inventory` WHERE `user_id` = @id AND `slot` = @slot LIMIT 1");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        var row = dbClient.GetRow();
        return row == null ? ("", 0) : (Convert.ToString(row["item"]) ?? "", Convert.ToInt32(row["wear"]));
    }

    /// <summary>Set the item's wear, or take it away when that is its limit. True when it broke.</summary>
    private static bool Wear(int userId, int slot, string item, int wear)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        var broke = wear >= MaxOf(item);
        dbClient.SetQuery(broke
            ? "DELETE FROM `user_rp_inventory` WHERE `user_id` = @id AND `slot` = @slot AND `item` = @item"
            : "UPDATE `user_rp_inventory` SET `wear` = @wear WHERE `user_id` = @id AND `slot` = @slot AND `item` = @item");
        dbClient.AddParameter("id", userId);
        dbClient.AddParameter("slot", slot);
        dbClient.AddParameter("item", item);
        dbClient.AddParameter("wear", wear);
        dbClient.RunQuery();
        return broke;
    }

    /// <summary>The bar on the item moved, or the item broke: the owner sees it.</summary>
    private static void Tell(Habbo habbo, string? broken, bool weapon)
    {
        var client = habbo.Client;
        if (client == null)
            return;
        var inventory = habbo.LoadRpInventory();
        if (broken != null)
        {
            // a broken weapon leaves the hand with it, broken armour the vest
            if (weapon)
                RpWeapons.ApplyToHand(habbo, inventory);
            else
                SyncVest(habbo, inventory);
            client.SendWhisper($"Your {NameOf(broken)} broke.");
        }
        client.Send(new RpInventoryComposer(inventory));
    }
}
