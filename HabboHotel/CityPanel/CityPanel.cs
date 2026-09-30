using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.HabboHotel.CityPanel;

/// <summary>
/// pixelrp: the City Panel - the staff window, opened from Mod Tools, for
/// managing the city (design: the "City Panel" canvas).
///
/// Opening it needs `rp_city_panel` (rank 5, migration 232). Each action also
/// needs the permission of the command it mirrors, so a rank means what it
/// meant before the panel: whoever cannot :kill cannot kill from here either.
/// The client is told which actions it may offer (<see cref="Capabilities"/>),
/// and every packet checks again.
/// </summary>
public static class CityPanelAccess
{
    public const string OpenPermission = "rp_city_panel";

    [Flags]
    public enum Capability
    {
        None = 0,
        Restore = 1,
        Kill = 2,
        Summon = 4,
        GoTo = 8,
        /// <summary>Release from jail and clear charges.</summary>
        Justice = 16,
        Balance = 32,
        Backpack = 64,
        Uniforms = 128,
        AlertHotel = 256,
        AlertStaff = 512,
        AlertRoom = 1024,
        /// <summary>The sky: weather and time of day.</summary>
        World = 2048,
        /// <summary>Maintenance and the combat switch.</summary>
        Hotel = 4096
    }

    private static readonly (Capability Capability, string Permission)[] Permissions =
    {
        (Capability.Restore, "command_restore"),
        (Capability.Kill, "command_kill"),
        (Capability.Summon, "command_summon"),
        (Capability.GoTo, "command_goto"),
        (Capability.Justice, "rp_city_justice"),
        (Capability.Balance, "rp_city_balance"),
        (Capability.Backpack, "command_spawn"),
        (Capability.Uniforms, "rp_city_uniforms"),
        (Capability.AlertHotel, "command_hotel_alert"),
        (Capability.AlertStaff, "command_staff_alert"),
        (Capability.AlertRoom, "command_room_alert"),
        (Capability.World, "rp_city_world"),
        (Capability.Hotel, "rp_city_hotel")
    };

    public static bool CanOpen(Habbo? habbo) => habbo != null && habbo.Permissions.HasCommand(OpenPermission);

    public static Capability Capabilities(Habbo? habbo)
    {
        if (!CanOpen(habbo))
            return Capability.None;
        var capabilities = Capability.None;
        foreach (var (capability, permission) in Permissions)
            if (habbo!.Permissions.HasCommand(permission))
                capabilities |= capability;
        return capabilities;
    }

    public static bool Can(Habbo? habbo, Capability capability) => (Capabilities(habbo) & capability) == capability;
}

/// <summary>One row of a City Panel player search.</summary>
public sealed class CityPlayerRow
{
    public int Id { get; init; }
    public string Username { get; init; } = "";
    public string Look { get; init; } = "";
    public string Gender { get; init; } = "M";
    public bool Online { get; init; }
    public string Where { get; init; } = "";
    public CityPlayerFlags Flags { get; init; }
}

[Flags]
public enum CityPlayerFlags
{
    None = 0,
    Wanted = 1,
    Jailed = 2,
    OnDuty = 4,
    Cuffed = 8
}

/// <summary>Everything the Players tab shows about one player.</summary>
public sealed class CityPlayerCard
{
    public int Id { get; init; }
    public string Username { get; init; } = "";
    public string Look { get; init; } = "";
    public string Gender { get; init; } = "M";
    public bool Online { get; init; }
    public string Where { get; init; } = "";
    public int Health { get; init; }
    public int HealthMax { get; init; }
    public int Energy { get; init; }
    public int EnergyMax { get; init; }
    public int Aggression { get; init; }
    public int OpenCharges { get; init; }
    public int JailSecondsLeft { get; init; }
    public bool Cuffed { get; init; }
    public string Gang { get; init; } = "";
    public string Job { get; init; } = "";
    public bool OnDuty { get; init; }
    public int Credits { get; init; }
    public int UnlockedSlots { get; init; }
    public List<(int Slot, string Item, int Count)> Backpack { get; init; } = new();
}

/// <summary>
/// The Players tab: search, the player card, the staff actions and the
/// backpack editor. Online players are read and changed live; offline ones
/// through the same tables, so a sentence, a charge sheet, a balance or a
/// backpack can be put right without waiting for them to log in.
/// </summary>
public static class CityPlayers
{
    public const int FilterAll = 0;
    public const int FilterOnline = 1;
    public const int FilterWanted = 2;
    public const int FilterJailed = 3;

    private const int SearchLimit = 50;

    public static List<CityPlayerRow> Search(string query, int filter)
    {
        query = (query ?? "").Replace("%", "").Replace("_", "").Trim();
        if (query.Length > 32)
            query = query[..32];
        var clients = PlusEnvironment.Game.ClientManager;

        // Wanted and jailed are sets; online is the client list; the rest is a
        // name search that also finds players who are offline.
        HashSet<int>? only = null;
        if (filter == FilterWanted)
        {
            WantedUtility.ExpireLapsed();
            only = WantedUtility.GetWanted().Select(wanted => wanted.UserId).ToHashSet();
        }
        else if (filter == FilterJailed)
        {
            only = JailState.ServingIds.ToHashSet();
            using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
            dbClient.SetQuery("SELECT DISTINCT `user_id` FROM `rp_jail` WHERE `released_at` = 0");
            var table = dbClient.GetTable();
            if (table != null)
                foreach (System.Data.DataRow row in table.Rows)
                    only.Add(Convert.ToInt32(row["user_id"]));
        }

        var rows = new List<CityPlayerRow>();
        if (filter == FilterOnline || (filter == FilterAll && query.Length == 0))
        {
            foreach (var client in clients.GetClients.ToList())
            {
                var habbo = client?.GetHabbo();
                if (habbo == null || !Matches(habbo.Username, query))
                    continue;
                rows.Add(RowFor(habbo.Id, habbo.Username, habbo.Look, habbo.Gender, habbo));
            }
        }
        else if (only != null)
        {
            foreach (var id in only)
            {
                var habbo = clients.GetClientByUserId(id)?.GetHabbo();
                if (habbo != null)
                {
                    if (Matches(habbo.Username, query))
                        rows.Add(RowFor(habbo.Id, habbo.Username, habbo.Look, habbo.Gender, habbo));
                    continue;
                }
                var offline = LoadUser(id);
                if (offline != null && Matches(offline.Value.Username, query))
                    rows.Add(RowFor(id, offline.Value.Username, offline.Value.Look, offline.Value.Gender, null));
            }
        }
        else
        {
            using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
            dbClient.SetQuery("SELECT `id`, `username`, `look`, `gender` FROM `users` WHERE `username` LIKE @query ORDER BY `username` LIMIT " + SearchLimit);
            dbClient.AddParameter("query", query + "%");
            var table = dbClient.GetTable();
            if (table != null)
                foreach (System.Data.DataRow row in table.Rows)
                {
                    var id = Convert.ToInt32(row["id"]);
                    rows.Add(RowFor(id, Convert.ToString(row["username"]) ?? "", Convert.ToString(row["look"]) ?? "",
                        Convert.ToString(row["gender"]) ?? "M", clients.GetClientByUserId(id)?.GetHabbo()));
                }
        }

        // Online first, then by name - the people staff can act on right now.
        return rows.OrderByDescending(row => row.Online).ThenBy(row => row.Username, StringComparer.OrdinalIgnoreCase)
            .Take(SearchLimit).ToList();
    }

    private static bool Matches(string username, string query) =>
        query.Length == 0 || (username ?? "").StartsWith(query, StringComparison.OrdinalIgnoreCase);

    private static CityPlayerRow RowFor(int id, string username, string look, string gender, Habbo? online)
    {
        var flags = CityPlayerFlags.None;
        if (JailState.IsJailed(id))
            flags |= CityPlayerFlags.Jailed;
        if (online != null)
        {
            if (ShiftManager.IsOnDuty(id))
                flags |= CityPlayerFlags.OnDuty;
            if (PoliceState.IsCuffed(id))
                flags |= CityPlayerFlags.Cuffed;
        }
        return new CityPlayerRow
        {
            Id = id,
            Username = username,
            Look = look ?? "",
            Gender = string.IsNullOrEmpty(gender) ? "M" : gender.ToUpperInvariant(),
            Online = online != null,
            Where = online == null ? "Offline" : (online.CurrentRoom?.Name ?? "Between rooms"),
            Flags = flags
        };
    }

    private static (string Username, string Look, string Gender, int Credits, long VipExpire)? LoadUser(int userId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `username`, `look`, `gender`, `credits`, `vip_expire` FROM `users` WHERE `id` = @id LIMIT 1");
        dbClient.AddParameter("id", userId);
        var row = dbClient.GetRow();
        if (row == null)
            return null;
        return (Convert.ToString(row["username"]) ?? "", Convert.ToString(row["look"]) ?? "", Convert.ToString(row["gender"]) ?? "M",
            Convert.ToInt32(row["credits"]), Convert.ToInt64(row["vip_expire"]));
    }

    private static int UnlockedSlotsOffline(long vipExpire) =>
        vipExpire > DateTimeOffset.UtcNow.ToUnixTimeSeconds() ? Habbo.RpCarrySlots : Habbo.RpCarrySlotsBase;

    /// <summary>The player card, or null for a user id that does not exist.</summary>
    public static CityPlayerCard? Card(int userId)
    {
        var habbo = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.GetHabbo();
        int health, healthMax, energy, energyMax, aggression, credits, unlocked;
        string username, look, gender;
        if (habbo != null)
        {
            habbo.EnsureRpStatsLoaded();
            (username, look, gender) = (habbo.Username, habbo.Look, habbo.Gender);
            (health, healthMax, energy, energyMax) = (habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax);
            aggression = (int)Math.Round(habbo.RpAggression);
            credits = habbo.Credits;
            unlocked = habbo.RpUnlockedSlots;
        }
        else
        {
            var user = LoadUser(userId);
            if (user == null)
                return null;
            (username, look, gender, credits) = (user.Value.Username, user.Value.Look, user.Value.Gender, user.Value.Credits);
            unlocked = UnlockedSlotsOffline(user.Value.VipExpire);
            (health, healthMax, energy, energyMax) = (100, 100, 100, 100);
            using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
            dbClient.SetQuery("SELECT `health`, `health_max`, `energy`, `energy_max` FROM `user_rp_stats` WHERE `user_id` = @id LIMIT 1");
            dbClient.AddParameter("id", userId);
            var stats = dbClient.GetRow();
            if (stats != null)
                (health, healthMax, energy, energyMax) = (Convert.ToInt32(stats["health"]), Convert.ToInt32(stats["health_max"]),
                    Convert.ToInt32(stats["energy"]), Convert.ToInt32(stats["energy_max"]));
            // Aggression is transient: it drains to nothing, and it is never saved.
            aggression = 0;
        }

        int charges;
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT COUNT(*) FROM `rp_charges` WHERE `user_id` = @id AND `dropped_at` = 0");
            dbClient.AddParameter("id", userId);
            charges = dbClient.GetInteger();
        }

        var employment = CorporationUtility.GetEmployment(userId);
        var gang = GangUtility.GetGang(userId);
        return new CityPlayerCard
        {
            Id = userId,
            Username = username,
            Look = look ?? "",
            Gender = string.IsNullOrEmpty(gender) ? "M" : gender.ToUpperInvariant(),
            Online = habbo != null,
            Where = habbo == null ? "Offline" : (habbo.CurrentRoom?.Name ?? "Between rooms"),
            Health = health,
            HealthMax = healthMax,
            Energy = energy,
            EnergyMax = energyMax,
            Aggression = aggression,
            OpenCharges = charges,
            JailSecondsLeft = JailState.SecondsLeftAnywhere(userId),
            Cuffed = habbo != null && PoliceState.IsCuffed(userId),
            Gang = gang?.Name ?? "",
            Job = employment == null || employment.CorpId == 0 ? "" : $"{employment.CorpName} · {employment.RankName}",
            OnDuty = habbo != null && ShiftManager.IsOnDuty(userId),
            Credits = credits,
            UnlockedSlots = unlocked,
            Backpack = RpInventoryStore.Load(userId)
        };
    }

    /// <summary>
    /// Move coins on hand by `delta` (never below zero). Online players are
    /// changed in memory - the logout save writes that value - and told;
    /// either way the row moves by the same amount now, so a crash in between
    /// loses nothing. Returns the new balance.
    /// </summary>
    public static int AdjustCredits(int userId, int delta)
    {
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        var habbo = client?.GetHabbo();
        if (habbo != null)
        {
            delta = Math.Max(delta, -habbo.Credits);
            habbo.Credits += delta;
            client!.Send(new CreditBalanceComposer(habbo.Credits));
        }
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `users` SET `credits` = GREATEST(0, `credits` + @delta) WHERE `id` = @id LIMIT 1");
        dbClient.AddParameter("delta", delta);
        dbClient.AddParameter("id", userId);
        dbClient.RunQuery();
        if (habbo != null)
            return habbo.Credits;
        dbClient.SetQuery("SELECT `credits` FROM `users` WHERE `id` = @id LIMIT 1");
        dbClient.AddParameter("id", userId);
        return dbClient.GetInteger();
    }

    /// <summary>Full health and energy for a player who is offline (online ones go through :restore).</summary>
    public static void RestoreOffline(int userId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `user_rp_stats` SET `health` = `health_max`, `energy` = `energy_max` WHERE `user_id` = @id LIMIT 1");
        dbClient.AddParameter("id", userId);
        dbClient.RunQuery();
    }

    // ---- the backpack --------------------------------------------------------

    public const int BackpackGive = 1;
    public const int BackpackRemove = 2;
    public const int BackpackSetCount = 3;

    /// <summary>
    /// One backpack edit. The item keys are the :spawn list, and every rule the
    /// game already holds holds here: weapons never stack, nobody holds two
    /// pairs of handcuffs, a stack stops at ten, and a full backpack is full.
    /// Returns what to tell the staff member, or null when it went through.
    /// </summary>
    public static string? EditBackpack(int userId, int op, int slot, string item, int count)
    {
        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        var habbo = client?.GetHabbo();
        var unlocked = habbo?.RpUnlockedSlots ?? UnlockedSlotsOffline(LoadUser(userId)?.VipExpire ?? 0);
        var weaponTouched = false;
        switch (op)
        {
            case BackpackGive:
            {
                if (!SpawnCommand.IsSpawnable(item))
                    return "That is not an item that can be given.";
                var given = RpInventoryStore.Add(userId, item, unlocked);
                if (given == Habbo.RpAlreadyHeld)
                    return "They already hold one, and may only hold one.";
                if (given < 0)
                    return "Their backpack is full.";
                break;
            }
            case BackpackRemove:
            {
                var removed = RpInventoryStore.Discard(userId, slot, int.MaxValue);
                if (removed.Item == null)
                    return "That slot is already empty.";
                weaponTouched = slot == RpWeapons.WeaponSlot;
                break;
            }
            case BackpackSetCount:
                if (!RpInventoryStore.SetCount(userId, slot, count))
                    return "That slot is empty.";
                weaponTouched = slot == RpWeapons.WeaponSlot;
                break;
            default:
                return "Unknown backpack change.";
        }

        if (habbo != null)
        {
            var inventory = habbo.LoadRpInventory();
            if (weaponTouched)
                RpWeapons.ApplyToHand(habbo, inventory);
            client!.Send(new RpInventoryComposer(inventory));
        }
        return null;
    }

    /// <summary>Staff may not use the panel on their own characters - same rule as :pardon and :charge.</summary>
    public static bool IsOwnCharacter(Habbo staff, int userId) => staff.Id == userId || AccountUtility.SameAccount(staff.Id, userId);
}
