using System.Collections;
using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Navigator;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Subscriptions;
using Plus.HabboHotel.Users.Clothing;
using Plus.HabboHotel.Users.Effects;
using Plus.HabboHotel.Users.Ignores;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Messenger;
using Plus.HabboHotel.Users.Messenger.FriendBar;
using Plus.HabboHotel.Users.Permissions;
using Plus.HabboHotel.Users.Process;
using Plus.Utilities;

using Dapper;
using Plus.HabboHotel.Users.Navigator;

namespace Plus.HabboHotel.Users;

public class Habbo
{
    public HabboStats HabboStats { get; set; }

    private readonly DateTime _timeCached;

    public GameClient Client { get; set; }
    public ClothingComponent Clothing { get; set; }

    private bool _disconnected;
    public EffectsComponent Effects { get; set; }

    private bool _habboSaved;

    public IgnoresComponent IgnoresComponent { get; set; }
    public InventoryComponent Inventory { get; set; }

    public HabboMessenger Messenger { get; set; }

    public NavigatorPreferences NavigatorPreferences { get; set; }
    public PermissionComponent Permissions { get; set; }

    [Obsolete("Should be deleted /refactored to standalone service")]
    private ProcessComponent Process { get; set; }

    public ConcurrentDictionary<string, UserAchievement> Achievements = new();
    public ArrayList FavoriteRooms = new();
    public Dictionary<int, int> Quests = new();

    public List<uint> RatedRooms = new();

    // TODO @80O: Convert to uint
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public int Rank { get; set; }

    // Staff cutoff mirrors the Nitro client's SecurityLevel.MODERATOR (isModerator),
    // which the UI already uses to gate the rooms/catalog/inventory/camera icons.
    public bool IsStaff => Rank >= 5;

    // Full-wardrobe cutoff: rank 4+ can wear any sellable clothing set
    // without owning a user_clothing row (see FullWardrobeUtility).
    public bool HasFullWardrobe => Rank >= 4;

    public bool IsAmbassador { get; set; }

    public string Motto { get; set; } = string.Empty;

    public string Look { get; set; } = string.Empty;

    public string Gender { get; set; } = string.Empty;

    public int Credits { get; set; }

    public int Duckets { get; set; }

    public int Diamonds { get; set; }

    public int GotwPoints { get; set; }

    public uint HomeRoom { get; set; }

    // pixelrp last-position restore: set once at login when the user is
    // forwarded to their last room; consumed (and cleared) by AddAvatarToRoom.
    public PendingRoomRestore PendingRestore { get; set; }

    public double LastOnline { get; set; }

    public double AccountCreated { get; set; }

    public List<int> ClientVolume { get; set; } = new() { 0, 0, 0 };

    public double LastNameChange { get; set; }

    public string MachineId { get; set; }

    public bool ChatPreference { get; set; }

    public bool FocusPreference { get; set; }

    // pixelrp: VIP is time-based. vip_expire (unix seconds) is the source of
    // truth; rank_vip is no longer read. VipRank is derived so every legacy
    // VipRank gate (permissions_subscriptions, catalog min_vip, respect
    // allowance) keys off live VIP status.
    public long VipExpire { get; set; }
    public DateTime? VipLastStipend { get; set; }
    public bool IsVip => VipExpire > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public int VipRank => IsVip ? 1 : 0;

    public bool AllowTradingRequests { get; set; }

    public bool AllowUserFollowing { get; set; }

    public bool AllowMessengerInvites { get; set; }

    public bool AllowPetSpeech { get; set; }

    public bool AllowBotSpeech { get; set; }

    public bool AllowConsoleMessages { get; set; } = true;

    public bool AllowGifts { get; set; }

    public bool AllowMimic { get; set; }

    public bool ReceiveWhispers { get; set; }

    public bool IgnorePublicWhispers { get; set; }

    public FriendBarState FriendbarState { get; set; }

    public int TimeAfk { get; set; }

    public bool DisableForcedEffects { get; set; }

    // pixelrp: airplane mode - hides incoming friend requests and bounces DMs
    // sent to this player. Loaded from users.airplane_mode; toggled via the
    // phone's Settings app.
    public bool AirplaneMode { get; set; }

    public bool ChangingName { get; set; }

    public double FloodTime { get; set; }

    public int BannedPhraseCount { get; set; }

    public bool RoomAuthOk { get; set; }

    public int QuestLastCompleted { get; set; }

    public int MessengerSpamCount { get; set; }

    public double MessengerSpamTime { get; set; }

    public double TimeMuted { get; set; }

    public double TradingLockExpiry { get; set; }

    public double SessionStart { get; set; }

    public uint TentId { get; set; }

    public uint HopperId { get; set; }

    public bool IsHopping { get; set; }

    public uint TeleporterId { get; set; }

    public bool IsTeleporting { get; set; }

    public uint TeleportingRoomId { get; set; }

    /// <summary>
    /// Last room the server told this client to enter (RoomForwardComposer). Non-staff
    /// clients may only open a flat connection to a server-authorized target — anything
    /// else is an injected packet bypassing the staff-only navigator.
    /// </summary>
    public uint AuthorizedRoomEntryId { get; set; }

    public bool HasSpoken { get; set; }

    public double LastAdvertiseReport { get; set; }

    public bool AdvertisingReported { get; set; }

    public bool AdvertisingReportedBlocked { get; set; }

    public bool WiredInteraction { get; set; }

    public int CustomBubbleId { get; set; }

    // pixelrp RP stats (health/energy shown in the player HUD). Lazy-loaded
    // from `user_rp_stats` on first use (row created with 100/100 defaults);
    // values only change via explicit mutation (:sethp / :seten and future RP
    // systems) — no regen. See EnsureRpStatsLoaded/SaveRpStats.
    public bool RpStatsLoaded { get; set; }
    public int RpHealth { get; set; } = 100;
    public int RpHealthMax { get; set; } = 100;
    public int RpEnergy { get; set; } = 100;
    public int RpEnergyMax { get; set; } = 100;

    // Aggression is transient (not persisted): set via :setagg (or future RP
    // systems) and drained by the room tick at 100 points per 45 seconds.
    // Stored as double so the per-tick decay can be fractional.
    public double RpAggression { get; set; }

    // Passive status (consumable smoothie): remaining ONLINE seconds.
    // Persisted in user_rp_stats.passive_seconds; decremented by the room
    // tick while the player is in a room (see RoomUserManager.OnCycle).
    // RpPassiveLastTick is the transient decrement clock.
    public int RpPassiveSeconds { get; set; }
    public long RpPassiveLastTick { get; set; }

    public void EnsureRpStatsLoaded()
    {
        if (RpStatsLoaded)
            return;
        RpStatsLoaded = true;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `health`,`health_max`,`energy`,`energy_max`,`passive_seconds` FROM `user_rp_stats` WHERE `user_id` = @id LIMIT 1");
        dbClient.AddParameter("id", Id);
        var row = dbClient.GetRow();
        if (row == null)
        {
            dbClient.SetQuery("INSERT INTO `user_rp_stats` (`user_id`,`health`,`health_max`,`energy`,`energy_max`) VALUES (@id,100,100,100,100)");
            dbClient.AddParameter("id", Id);
            dbClient.RunQuery();
            return;
        }
        RpHealth = Convert.ToInt32(row["health"]);
        RpHealthMax = Convert.ToInt32(row["health_max"]);
        RpEnergy = Convert.ToInt32(row["energy"]);
        RpEnergyMax = Convert.ToInt32(row["energy_max"]);
        RpPassiveSeconds = Convert.ToInt32(row["passive_seconds"]);
    }

    public void SaveRpStats()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `user_rp_stats` SET `health` = @hp, `health_max` = @hpmax, `energy` = @en, `energy_max` = @enmax, `passive_seconds` = @passive WHERE `user_id` = @id");
        dbClient.AddParameter("hp", RpHealth);
        dbClient.AddParameter("hpmax", RpHealthMax);
        dbClient.AddParameter("en", RpEnergy);
        dbClient.AddParameter("enmax", RpEnergyMax);
        dbClient.AddParameter("passive", RpPassiveSeconds);
        dbClient.AddParameter("id", Id);
        dbClient.RunQuery();
    }

    // pixelrp UI settings: the player's chosen UI chrome color scheme
    // ("#rrggbb", empty = client default). Lazily loaded like the RP stats;
    // sent to the client at login (RpUiSettingsComposer) and saved when the
    // client picks a scheme (RpSaveUiSettingsEvent).
    public bool RpUiSettingsLoaded { get; set; }
    public string RpUiChromeColor { get; set; } = "";
    public int RpUiChromeOpacity { get; set; } = 95;
    public string RpUiHeaderColor { get; set; } = "";
    public string RpUiUsernameColor { get; set; } = "";
    public string RpUiUsernameIcon { get; set; } = "";
    public string RpUiUsernameIconColor { get; set; } = "";

    public void EnsureRpUiSettingsLoaded()
    {
        if (RpUiSettingsLoaded)
            return;
        RpUiSettingsLoaded = true;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `chrome_color`,`chrome_opacity`,`header_color`,`username_color`,`username_icon`,`username_icon_color` FROM `user_ui_settings` WHERE `user_id` = @id LIMIT 1");
        dbClient.AddParameter("id", Id);
        var row = dbClient.GetRow();
        if (row == null)
            return;
        RpUiChromeColor = Convert.ToString(row["chrome_color"]) ?? "";
        RpUiChromeOpacity = Convert.ToInt32(row["chrome_opacity"]);
        RpUiHeaderColor = Convert.ToString(row["header_color"]) ?? "";
        RpUiUsernameColor = Convert.ToString(row["username_color"]) ?? "";
        RpUiUsernameIcon = Convert.ToString(row["username_icon"]) ?? "";
        RpUiUsernameIconColor = Convert.ToString(row["username_icon_color"]) ?? "";
    }

    public void SaveRpUiSettings()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("REPLACE INTO `user_ui_settings` (`user_id`,`chrome_color`,`chrome_opacity`,`header_color`,`username_color`,`username_icon`,`username_icon_color`) VALUES (@id,@color,@opacity,@header,@username,@icon,@iconcolor)");
        dbClient.AddParameter("id", Id);
        dbClient.AddParameter("color", RpUiChromeColor);
        dbClient.AddParameter("opacity", RpUiChromeOpacity);
        dbClient.AddParameter("header", RpUiHeaderColor);
        dbClient.AddParameter("username", RpUiUsernameColor);
        dbClient.AddParameter("icon", RpUiUsernameIcon);
        dbClient.AddParameter("iconcolor", RpUiUsernameIconColor);
        dbClient.RunQuery();
    }

    // pixelrp RP inventory (backpack carry slots 1-10). No caching — reads
    // and writes go straight to user_rp_inventory; the client is refreshed
    // with RpInventoryComposer after every change.
    // pixelrp: 12 physical slots (the client renders 12); the last two unlock
    // while VIP is active. Soft lapse: items already in 11-12 stay usable and
    // consumable after expiry, but nothing new can be placed there.
    public const int RpCarrySlots = 12;
    public const int RpCarrySlotsBase = 10;
    public int RpUnlockedSlots => IsVip ? RpCarrySlots : RpCarrySlotsBase;

    public List<(int Slot, string Item, int Count)> LoadRpInventory()
    {
        var list = new List<(int, string, int)>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `slot`,`item`,`count` FROM `user_rp_inventory` WHERE `user_id` = @id ORDER BY `slot`");
        dbClient.AddParameter("id", Id);
        var table = dbClient.GetTable();
        if (table != null)
            foreach (System.Data.DataRow row in table.Rows)
                list.Add((Convert.ToInt32(row["slot"]), Convert.ToString(row["item"]), Convert.ToInt32(row["count"])));
        return list;
    }

    /// <summary>Adds one of an item (stacking onto an existing slot of the
    /// same item, else the first free carry slot). Returns the slot, or -1
    /// when the backpack is full.</summary>
    // A stack holds at most this many; the next item overflows into a free
    // slot (or fails as backpack-full like any other add).
    public const int RpStackCap = 10;

    public int AddRpItem(string item)
    {
        var inventory = LoadRpInventory();
        var existing = inventory.FirstOrDefault(entry => entry.Item == item && entry.Count < RpStackCap);
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        if (existing.Item == item && existing.Slot > 0 && existing.Slot <= RpUnlockedSlots)
        {
            dbClient.SetQuery("UPDATE `user_rp_inventory` SET `count` = `count` + 1 WHERE `user_id` = @id AND `slot` = @slot");
            dbClient.AddParameter("id", Id);
            dbClient.AddParameter("slot", existing.Slot);
            dbClient.RunQuery();
            return existing.Slot;
        }
        var used = inventory.Select(entry => entry.Slot).ToHashSet();
        var slot = Enumerable.Range(1, RpUnlockedSlots).FirstOrDefault(candidate => !used.Contains(candidate));
        if (slot == 0)
            return -1;
        dbClient.SetQuery("INSERT INTO `user_rp_inventory` (`user_id`,`slot`,`item`,`count`) VALUES (@id,@slot,@item,1)");
        dbClient.AddParameter("id", Id);
        dbClient.AddParameter("slot", slot);
        dbClient.AddParameter("item", item);
        dbClient.RunQuery();
        return slot;
    }

    /// <summary>Moves the backpack item in `from` into `to`, swapping when the
    /// target slot is occupied. Rows keep their counts; the three-step dance
    /// through temp slot 0 (never a real slot - they're 1-based) satisfies the
    /// (user_id, slot) primary key.</summary>
    public void MoveRpItem(int from, int to)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `user_rp_inventory` SET `slot` = 0 WHERE `user_id` = @id AND `slot` = @from");
        dbClient.AddParameter("id", Id);
        dbClient.AddParameter("from", from);
        dbClient.RunQuery();
        dbClient.SetQuery("UPDATE `user_rp_inventory` SET `slot` = @from WHERE `user_id` = @id AND `slot` = @to");
        dbClient.AddParameter("id", Id);
        dbClient.AddParameter("from", from);
        dbClient.AddParameter("to", to);
        dbClient.RunQuery();
        dbClient.SetQuery("UPDATE `user_rp_inventory` SET `slot` = @to WHERE `user_id` = @id AND `slot` = 0");
        dbClient.AddParameter("id", Id);
        dbClient.AddParameter("to", to);
        dbClient.RunQuery();
    }

    /// <summary>Removes one of whatever sits in the slot. Returns the item
    /// key, or null when the slot is empty.</summary>
    public string ConsumeRpItem(int slot)
    {
        var entry = LoadRpInventory().FirstOrDefault(candidate => candidate.Slot == slot);
        if (string.IsNullOrEmpty(entry.Item))
            return null;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        if (entry.Count > 1)
            dbClient.SetQuery("UPDATE `user_rp_inventory` SET `count` = `count` - 1 WHERE `user_id` = @id AND `slot` = @slot");
        else
            dbClient.SetQuery("DELETE FROM `user_rp_inventory` WHERE `user_id` = @id AND `slot` = @slot");
        dbClient.AddParameter("id", Id);
        dbClient.AddParameter("slot", slot);
        dbClient.RunQuery();
        return entry.Item;
    }

    public int FastfoodScore { get; set; }

    public int PetId { get; set; }

    public int CreditsUpdateTick { get; set; }

    public ICommandBase ChatCommand { get; set; }

    public DateTime LastGiftPurchaseTime { get; set; }

    public DateTime LastMottoUpdateTime { get; set; }

    public DateTime LastClothingUpdateTime { get; set; }

    public int GiftPurchasingWarnings { get; set; }

    public int MottoUpdateWarnings { get; set; }

    public int ClothingUpdateWarnings { get; set; }

    public bool SessionGiftBlocked { get; set; }

    public bool SessionMottoBlocked { get; set; }

    public bool SessionClothingBlocked { get; set; }

    public bool InRoom => CurrentRoom != null;

    public Room? CurrentRoom { get; set; }

    public string GetQueryString
    {
        get
        {
            _habboSaved = true;
            return
                $"UPDATE `users` SET `online` = false, `last_online` = '{UnixTimestamp.GetNow()}', `activity_points` = '{Duckets}', `credits` = '{Credits}', `vip_points` = '{Diamonds}', `home_room` = '{HomeRoom}', `gotw_points` = '{GotwPoints}', `time_muted` = '{TimeMuted}',`friend_bar_state` = '{FriendBarStateUtility.GetInt(FriendbarState)}' WHERE id = '{Id}' LIMIT 1;UPDATE `user_statistics` SET `roomvisits` = '{HabboStats.RoomVisits}', `onlineTime` = '{(UnixTimestamp.GetNow() - SessionStart + HabboStats.OnlineTime)}', `respect` = '{HabboStats.Respect}', `respectGiven` = '{HabboStats.RespectGiven}', `giftsGiven` = '{HabboStats.GiftsGiven}', `giftsReceived` = '{HabboStats.GiftsReceived}', `dailyRespectPoints` = '{HabboStats.DailyRespectPoints}', `dailyPetRespectPoints` = '{HabboStats.DailyPetRespectPoints}', `AchievementScore` = '{HabboStats.AchievementPoints}', `quest_id` = '{HabboStats.QuestId}', `quest_progress` = '{HabboStats.QuestProgress}', `groupid` = '{HabboStats.FavouriteGroupId}',`forum_posts` = '{HabboStats.ForumPosts}' WHERE `id` = '{Id}' LIMIT 1;";
        }
    }

    public bool CacheExpired()
    {
        var span = DateTime.Now - _timeCached;
        return span.TotalMinutes >= 30;
    }

    public bool InitProcess()
    {
        Process = new();
        return Process.Init(this);
    }

    public bool InitFx()
    {
        Effects = new();
        return Effects.Init(this);
    }

    public bool InitClothing()
    {
        Clothing = new();
        return Clothing.Init(this);
    }

    [Obsolete("Each loading task should be moved to their own IUserDataLoadingTask")]
    public void Init(GameClient client)
    {
        // Move each of these loading tasks to their own IUserDataLoadingTask implementation.
        //foreach (var id in data.FavouritedRooms) FavoriteRooms.Add(id);
        Client = client;
        //Quests = data.Quests;
        _disconnected = false;
        InitFx();
        InitClothing();
    }


    public event EventHandler? Disconnected;
    public void OnDisconnect()
    {
        if (_disconnected)
            return;

        Corporations.ShiftManager.InterruptForDisconnect(Id);

        Disconnected?.Invoke(this, EventArgs.Empty);

        try
        {
            if (Process != null)
                Process.Dispose();
        }
        catch { }
        _disconnected = true;
        PlusEnvironment.Game.ClientManager.UnregisterClient(Id, Username);
        if (!_habboSaved)
        {
            _habboSaved = true;
            using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
            dbClient.RunQuery(
                $"UPDATE `users` SET `online` = false, `last_online` = '{(int)UnixTimestamp.GetNow()}', `activity_points` = '{Duckets}', `credits` = '{Credits}', `vip_points` = '{Diamonds}', `home_room` = '{HomeRoom}', `gotw_points` = '{GotwPoints}', `time_muted` = '{TimeMuted}',`friend_bar_state` = '{FriendBarStateUtility.GetInt(FriendbarState)}', `bubble_id` = '{CustomBubbleId}' WHERE id = '{Id}' LIMIT 1;UPDATE `user_statistics` SET `roomvisits` = '{HabboStats.RoomVisits}', `onlineTime` = '{(int)(UnixTimestamp.GetNow() - SessionStart + HabboStats.OnlineTime)}', `respect` = '{HabboStats.Respect}', `respectGiven` = '{HabboStats.RespectGiven}', `giftsGiven` = '{HabboStats.GiftsGiven}', `giftsReceived` = '{HabboStats.GiftsReceived}', `dailyRespectPoints` = '{HabboStats.DailyRespectPoints}', `dailyPetRespectPoints` = '{HabboStats.DailyPetRespectPoints}', `AchievementScore` = '{HabboStats.AchievementPoints}', `quest_id` = '{HabboStats.QuestId}', `quest_progress` = '{HabboStats.QuestProgress}', `groupid` = '{HabboStats.FavouriteGroupId}',`forum_posts` = '{HabboStats.ForumPosts}' WHERE `id` = '{Id}' LIMIT 1;");
            if (Permissions.HasRight("mod_tickets"))
                dbClient.RunQuery($"UPDATE `moderation_tickets` SET `status` = 'open', `moderator_id` = '0' WHERE `status` ='picked' AND `moderator_id` = '{Id}'");
        }
        // pixelrp discord sync: drop the Online role shortly after logout.
        Plus.HabboHotel.Discord.DiscordSyncUtility.Enqueue(Id, "logout");
        Dispose();
        Client = null;
    }

    public void Dispose()
    {
        if (InRoom && CurrentRoom != null)
            CurrentRoom.GetRoomUserManager().RemoveUserFromRoom(Client, false);
        if (Effects != null)
            Effects.Dispose();
        if (Clothing != null)
            Clothing.Dispose();
        if (Permissions != null)
            Permissions.Dispose();
    }

    public void CheckCreditsTimer()
    {
        try
        {
            CreditsUpdateTick--;
            if (CreditsUpdateTick <= 0)
            {
                var creditUpdate = Convert.ToInt32(PlusEnvironment.SettingsManager.TryGetValue("user.currency_scheduler.credit_reward"));
                var ducketUpdate = Convert.ToInt32(PlusEnvironment.SettingsManager.TryGetValue("user.currency_scheduler.ducket_reward"));
                SubscriptionData subData = null;
                if (PlusEnvironment.Game.SubscriptionManager.TryGetSubscriptionData(VipRank, out subData))
                {
                    creditUpdate += subData.Credits;
                    ducketUpdate += subData.Duckets;
                }
                Credits += creditUpdate;
                Duckets += ducketUpdate;
                Client.Send(new CreditBalanceComposer(Credits));
                Client.Send(new HabboActivityPointNotificationComposer(Duckets, ducketUpdate));
                CreditsUpdateTick = Convert.ToInt32(PlusEnvironment.SettingsManager.TryGetValue("user.currency_scheduler.tick"));
            }
        }
        catch { }
    }


    public int GetQuestProgress(int p)
    {
        Quests.TryGetValue(p, out var progress);
        return progress;
    }

    public UserAchievement GetAchievementData(string p)
    {
        Achievements.TryGetValue(p, out var achievement);
        return achievement;
    }

    public void ChangeName(string username)
    {
        LastNameChange = UnixTimestamp.GetNow();
        Username = username;
        SaveKey("username", username);
        SaveKey("last_change", LastNameChange.ToString());
    }

    public void SaveChatBubble(string customBubbleId) => SaveKey("bubble_id", customBubbleId);

    public void SaveKey(string key, string value)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery($"UPDATE `users` SET {key} = @value WHERE `id` = '{Id}' LIMIT 1;");
        dbClient.AddParameter("value", value);
        dbClient.RunQuery();
    }

    public void PrepareRoom(uint id, string password)
    {
        if (Client == null || Client.GetHabbo() == null)
            return;

        if (Client.GetHabbo().InRoom)
        {
            var oldRoom = Client.GetHabbo().CurrentRoom;
            // A disposed room (unloaded while we were still in it, e.g. a floor
            // plan save) has no user manager left; clear the stale reference or
            // every future room entry NREs and the client stays on a black screen.
            if (oldRoom != null && !oldRoom.MDisposed)
                oldRoom.GetRoomUserManager().RemoveUserFromRoom(Client, false);
            else
                Client.GetHabbo().CurrentRoom = null;
        }
        if (Client.GetHabbo().IsTeleporting && Client.GetHabbo().TeleportingRoomId != id)
        {
            Client.Send(new CloseConnectionComposer());
            return;
        }
        if (!PlusEnvironment.Game.RoomManager.TryLoadRoom(id, out var room))
        {
            Client.Send(new CloseConnectionComposer());
            return;
        }
        if (room.IsCrashed)
        {
            Client.SendNotification("This room has crashed! :(");
            Client.Send(new CloseConnectionComposer());
            return;
        }
        if (room.GetRoomUserManager().UserCount >= room.UsersMax && !Client.GetHabbo().Permissions.HasRight("room_enter_full") && Client.GetHabbo().Id != room.OwnerId)
        {
            Client.Send(new CantConnectComposer(1));
            Client.Send(new CloseConnectionComposer());
            return;
        }
        if (!Permissions.HasRight("room_ban_override") && room.GetBans().IsBanned(Id))
        {
            RoomAuthOk = false;
            Client.GetHabbo().RoomAuthOk = false;
            Client.Send(new CantConnectComposer(4));
            Client.Send(new CloseConnectionComposer());
            return;
        }
        Client.Send(new OpenConnectionComposer());
        if (!room.CheckRights(Client, true, true) && !Client.GetHabbo().IsTeleporting && !Client.GetHabbo().IsHopping)
        {
            if (room.Access == RoomAccess.Doorbell && !Client.GetHabbo().Permissions.HasRight("room_enter_locked"))
            {
                if (room.UserCount > 0)
                {
                    Client.Send(new DoorbellComposer(""));
                    room.SendPacket(new DoorbellComposer(Client.GetHabbo().Username), true);
                    return;
                }
                Client.Send(new FlatAccessDeniedComposer(""));
                Client.Send(new CloseConnectionComposer());
                return;
            }
            if (room.Access == RoomAccess.Password && !Client.GetHabbo().Permissions.HasRight("room_enter_locked"))
            {
                if (password.ToLower() != room.Password.ToLower() || string.IsNullOrWhiteSpace(password))
                {
                    Client.Send(new GenericErrorComposer(-100002));
                    Client.Send(new CloseConnectionComposer());
                    return;
                }
            }
        }
        if (!EnterRoom(room))
            Client.Send(new CloseConnectionComposer());
    }

    public bool EnterRoom(Room room)
    {
        if (room == null)
            return false;
        Client.GetHabbo().CurrentRoom = room;
        Client.Send(new RoomReadyComposer(room.RoomId, room.ModelName));
        if (room.Wallpaper != "0.0")
            Client.Send(new RoomPropertyComposer("wallpaper", room.Wallpaper));
        if (room.Floor != "0.0")
            Client.Send(new RoomPropertyComposer("floor", room.Floor));
        Client.Send(new RoomPropertyComposer("landscape", room.Landscape));
        Client.Send(new RoomRatingComposer(room.Score, !(Client.GetHabbo().RatedRooms.Contains(room.RoomId) || room.OwnerId == Client.GetHabbo().Id)));
        using (var dbClient = PlusEnvironment.DatabaseManager.Connection())
        {
            dbClient.Execute("INSERT INTO user_roomvisits (user_id,room_id,entry_timestamp,exit_timestamp) VALUES (@userId, @roomId, @entryTimestamp, @exitTimestamp)",
                new
                {
                    userId = Client.GetHabbo().Id,
                    roomId = Client.GetHabbo().CurrentRoom.RoomId,
                    entryTimestamp = UnixTimestamp.GetNow(),
                    exitTimestamp = 0,
                });
        }

        if (room.OwnerId != Id)
        {
            Client.GetHabbo().HabboStats.RoomVisits += 1;
            PlusEnvironment.Game.AchievementManager.ProgressAchievement(Client, "ACH_RoomEntry", 1);
        }
        return true;
    }
}