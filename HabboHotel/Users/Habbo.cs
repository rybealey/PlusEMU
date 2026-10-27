using System.Collections;
using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Navigator;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Corporations;
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

    // pixelrp: orders a logout against a login's InitProcess, which can run at
    // the same moment on another thread - see InitProcess.
    private readonly object _logoutLock = new();
    private bool _logoutStarted;

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
    // which the UI already uses to gate the rooms icon and the camera.
    public bool IsStaff => Rank >= StaffRank;

    /// <summary>Administrator: the staff line IsStaff draws.</summary>
    public const int StaffRank = 5;

    /// <summary>Builder, the lowest staff rank (migration 285).</summary>
    public const int BuilderRank = 2;

    /// <summary>Moderator (migration 285).</summary>
    public const int ModeratorRank = 4;

    // pixelrp: every staff rank, Builder up, wears any clothing without owning
    // it - every Clothing Store piece and every face - in Choose Your Look and
    // on any look they save (FigureDataManager.ProcessFigure; 2026-10-22, was
    // Moderator up until 2026-10-27).
    // Client: WearsAnyClothing in RpRanks.
    public bool WearsAnyClothing => Rank >= BuilderRank;

    // pixelrp: Builder or above. The Furni Shop and the furni inventory open
    // from here rather than IsStaff, because Builders build (Twist, 2026-10-20),
    // and so does the navigator - room creation included (Twist, 2026-10-21).
    // Client: RP_RANK_BUILDER in ToolbarView.
    public bool IsBuilder => Rank >= BuilderRank;

    // pixelrp: renaming a furni and moving it to another category from the
    // Furni Shop, from Builder (Twist, 2026-10-21). Below the staff line only on
    // shelves that rank can buy from (RpCatalogRenameFurniEvent,
    // RpCatalogMoveFurniEvent). The Function tool stays on rp_furni_function.
    // Client: CanEditShop in RpRanks.
    public bool CanEditShop => IsBuilder;

    public bool IsAmbassador { get; set; }

    public string Motto { get; set; } = string.Empty;

    public string Look { get; set; } = string.Empty;

    // pixelrp uniforms (UniformManager): a uniform is worn in memory over the
    // player's own look, which is kept here meanwhile - users.look is never
    // written with a uniform, and nothing may save one while it is on.
    public bool WearingUniform { get; set; }
    public string? OwnLook { get; set; }
    // pixelrp body armour (RpDurability.SyncVest): worn in the backpack's
    // Armor slot, it puts the vest on the look the same way - in memory, over
    // the player's own (and any uniform).
    public bool WearingArmor { get; set; }
    // and the vest's colour: their gang's primary colour, matched to the
    // clothing palette (0 for none - the vest's own grey)
    public int VestColour { get; set; }

    public string Gender { get; set; } = string.Empty;

    // pixelrp: cash on hand - read freely, changed through ChangeCredits.
    private int _credits;

    public int Credits
    {
        get => Volatile.Read(ref _credits);
        set => Volatile.Write(ref _credits, value);
    }

    /// <summary>
    /// pixelrp: the way cash on hand changes - in memory and, as a RELATIVE
    /// update, in users.credits at once, so the database is never behind what
    /// a player holds.
    ///
    /// Cash used to be written whole at logout, last of all and after the
    /// player had already left the online list. A login in between read the
    /// balance from before whatever they had just spent or given away, and the
    /// two sessions' saves then fought over it - a way to duplicate cash. Now
    /// every change is written as it happens and the logout never writes cash,
    /// so no old total can land over a newer one. Atomic in memory: the owner's
    /// own packets and other players' (a :give, a sale, a wage) can land at once.
    /// </summary>
    public void ChangeCredits(int delta)
    {
        if (delta == 0)
            return;
        ChangeCreditsInMemory(delta);
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `users` SET `credits` = `credits` + @delta WHERE `id` = @id LIMIT 1");
        dbClient.AddParameter("delta", delta);
        dbClient.AddParameter("id", Id);
        dbClient.RunQuery();
    }

    /// <summary>
    /// pixelrp: only the in-memory half of ChangeCredits, for a caller that
    /// writes the same relative change to users.credits itself, inside its own
    /// lock or transaction (the bank, the Marketplace, wages).
    /// </summary>
    public void ChangeCreditsInMemory(int delta) => Interlocked.Add(ref _credits, delta);

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

    public bool AllowUserFollowing { get; set; }

    public bool AllowMessengerInvites { get; set; }

    public bool AllowPetSpeech { get; set; }

    public bool AllowBotSpeech { get; set; }

    public bool AllowConsoleMessages { get; set; } = true;

    public bool AllowGifts { get; set; }

    /// <summary>pixelrp: the phone is open on this client's screen. Lives on the
    /// session rather than the RoomUser so that walking into another room puts
    /// the handitem back - see RoomUserManager.AddUserToRoom.</summary>
    public bool PhoneOpen { get; set; }

    public bool AllowMimic { get; set; }

    public bool IgnorePublicWhispers { get; set; }

    public FriendBarState FriendbarState { get; set; }

    public int TimeAfk { get; set; }

    public bool DisableForcedEffects { get; set; }

    // pixelrp: airplane mode - hides incoming friend requests and bounces DMs
    // sent to this player. Loaded from users.airplane_mode; toggled via the
    // phone's Settings app.
    public bool AirplaneMode { get; set; }

    // pixelrp: which part of the world this player plays from - 'na', 'eu',
    // 'oc', or '' when they have not said (96_UserRegion.sql). Shown on their
    // profile beside the motto, so unlike the phone's own settings it lives on
    // the user row where anyone can read it.
    public string RpRegion { get; set; } = "";

    public bool ChangingName { get; set; }

    public double FloodTime { get; set; }

    /// <summary>pixelrp: unix seconds until THIS player can be alerted by another
    /// @mention. The timer belongs to the person being mentioned, not the speaker,
    /// so two people addressed in the same breath both get their ping - it only
    /// stops the same player being alerted over and over. Session-only and never
    /// persisted: a cooldown measured in seconds is not worth a database round
    /// trip, and a relog resets something about to expire anyway. See
    /// RoomUser.OnChat.</summary>
    public double MentionCooldownUntil { get; set; }

    public int BannedPhraseCount { get; set; }

    public bool RoomAuthOk { get; set; }

    public int QuestLastCompleted { get; set; }

    public int MessengerSpamCount { get; set; }

    public double MessengerSpamTime { get; set; }

    public double TimeMuted { get; set; }

    public double TradingLockExpiry { get; set; }

    // When this session began (unix seconds), set at login by Init. Upstream
    // set it in the old constructor; a 2022 refactor dropped that constructor
    // and nothing set it again, so it stayed 0 and every save added a whole
    // unix timestamp to user_statistics.onlineTime. PlusEnvironment's startup
    // clears the impossible values that left behind.
    public double SessionStart { get; set; }

    /// <summary>Total time online in seconds: every saved session plus this one so far.</summary>
    public int OnlineTimeTotal =>
        (HabboStats?.OnlineTime ?? 0) + (SessionStart > 0 ? (int)Math.Max(0, UnixTimestamp.GetNow() - SessionStart) : 0);

    public uint TentId { get; set; }

    public uint HopperId { get; set; }

    public bool IsHopping { get; set; }

    public uint TeleporterId { get; set; }

    public bool IsTeleporting { get; set; }

    public uint TeleportingRoomId { get; set; }

    /// <summary>
    /// A teleport or a hopper is one trip, and this is the end of it.
    ///
    /// The V1 movement engine cleared these on the player's first step out of
    /// the destination teleporter. V2 has no such step, and when V1 was deleted
    /// nothing took the job over - so a single ride left IsTeleporting set for
    /// the rest of the session, and PrepareRoom sent every later room change
    /// that was not back to the teleport's own room to the hotel view. Called
    /// on arrival, and on every way a teleport can end without one.
    /// </summary>
    public void EndTeleport()
    {
        IsTeleporting = false;
        TeleportingRoomId = 0;
        TeleporterId = 0;
        IsHopping = false;
        HopperId = 0;
    }

    /// <summary>
    /// Last room the server told this client to enter (RoomForwardComposer). Clients
    /// below Builder may only open a flat connection to a server-authorized target —
    /// anything else is an injected packet bypassing the navigator they don't have.
    /// </summary>
    public uint AuthorizedRoomEntryId { get; set; }

    /// <summary>
    /// pixelrp :backup: the room an officer answering a backup request is on
    /// their way to. Lets them through that room's lock, doorbell and user
    /// limit, once - PrepareRoom spends it on the next entry, wherever it is
    /// to (EmergencyCalls.GoToBackup).
    /// </summary>
    public uint BackupEntryRoomId { get; set; }

    /// <summary>
    /// pixelrp: the room the server has already sent this client, unasked, on
    /// entering it (RoomEntry, from EnterRoom) - so the client's own
    /// GetRoomEntryData for it is not answered a second time. 0 when a send
    /// failed and the client's request should be answered after all.
    /// </summary>
    public uint EntrySentRoomId { get; set; }

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

    // pixelrp gym (GymState): Strength and Stamina, levels 0-10, and the
    // seconds trained toward the next level. Stamina sets RpHealthMax. Saved
    // with the rest of the row by SaveRpStats.
    public int RpStrengthLevel { get; set; }
    public int RpStrengthSeconds { get; set; }
    public int RpStaminaLevel { get; set; }
    public int RpStaminaSeconds { get; set; }

    // Readings of RpOnlineSeconds: when the membership runs out, and when the
    // treadmill was last used (the Stamina drop's clock). RpStaminaDropped is
    // set by the drop and cleared by the next workout - it only drops once.
    public int RpGymUntil { get; set; }
    public int RpStaminaTrainedAt { get; set; }
    public bool RpStaminaDropped { get; set; }

    // The gym's own clock of time logged in (user_rp_stats.online_seconds).
    // Built while the hotel's user_statistics.onlineTime was broken (see
    // SessionStart), and kept separate: membership and the Stamina drop must
    // not move if that column is ever reset or edited by hand.
    // Runs from the stats load to logout (StopRpOnlineClock); only for a live
    // session - an offline Habbo loaded for a lookup never ticks.
    private int _rpOnlineBase;
    private long _rpOnlineAnchor;

    public int RpOnlineSeconds =>
        _rpOnlineBase + (_rpOnlineAnchor == 0 ? 0 : (int)((Environment.TickCount64 - _rpOnlineAnchor) / 1000));

    public void StopRpOnlineClock()
    {
        _rpOnlineBase = RpOnlineSeconds;
        _rpOnlineAnchor = 0;
    }

    // Aggression is transient (not persisted): set via :setagg (or future RP
    // systems) and drained by the room tick at 100 points per 45 seconds.
    // Stored as double so the per-tick decay can be fractional.
    public double RpAggression { get; set; }

    // pixelrp consumables: a snack refills energy, a medkit refills health,
    // each at the whole bar over a minute (RpRegen). Transient like aggression
    // - they run while the player is in a room and end at logout.
    //
    // Separate clocks on purpose: eating and bandaging at the same time is
    // fine, and a punch stops only the medkit.
    public RpRegen RpEnergyRegen { get; } = new();
    public RpRegen RpHealthRegen { get; } = new();

    // pixelrp: the "passive enable" avatar effect worn while RpPassiveSeconds > 0.
    // Maps to nitro/assets/bundled/effect/Squad.nitro via EffectMap.json id 597
    // (the canonical "Squad" effect id; matches the served EffectMap + clients).
    public const int PassiveEnableEffectId = 597;

    // pixelrp: the enable City Government wears while clocked in. It beats
    // every other enable for the shift (AvatarEffectActivatedEvent refuses
    // changes; the room tick re-applies it) and comes off at clock-out.
    public const int StaffDutyEffectId = 102;

    // pixelrp: :invisible (Invisibility). Nobody sees this player - their
    // avatar, chat, typing or enable - except others who are invisible too;
    // they still see and hear the whole room. Lives on the session, so it
    // follows them from room to room and ends at logout. The Ghost enable
    // beats every other one, the City Government enable included.
    public bool IsInvisible { get; set; }

    // Who made this player invisible with :invisible <name>; 0 when they
    // turned it on themselves. A player brought in who isn't staff can't turn
    // it off, so it ends with the one who brought them (Invisibility.Disable,
    // Invisibility.OnLeaveRoom).
    public int InvisibleBroughtById { get; set; }

    public const int InvisibleEffectId = 13;

    // pixelrp: passive is EITHER a smoothie still running OR being on duty
    // for City Government - staff are untouchable while working. Every
    // passive check reads this so the two sources can never drift apart.
    public bool IsRpPassive => RpPassiveSeconds > 0 || ShiftManager.IsStaffOnDuty(Id);

    // The attacking half of passive. A passive smoothie stops you fighting as
    // well as being fought; City Government on duty may fight (Twist,
    // 2026-10-15) - :hit, :slap, :spit, the stun gun, flashbangs, pepper spray -
    // while staying untouchable (IsRpPassive). Every attacker check asks this,
    // every target check asks IsRpPassive.
    public bool CannotFight => RpPassiveSeconds > 0;

    // Passive status (consumable smoothie): remaining ONLINE seconds.
    // Persisted in user_rp_stats.passive_seconds; decremented by the room
    // tick while the player is in a room (see RoomUserManager.OnCycle).
    // RpPassiveLastTick is the transient decrement clock.
    public int RpPassiveSeconds { get; set; }
    public long RpPassiveLastTick { get; set; }

    // Who the player has selected in their HUD, by user id - 0 for nobody.
    // The client reports every change (RpSetHudTargetEvent); a target
    // command typed without a name is aimed here (CommandManager). Transient.
    public int RpHudTargetId { get; set; }

    public void EnsureRpStatsLoaded()
    {
        if (RpStatsLoaded)
            return;
        RpStatsLoaded = true;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        // The gym's online clock starts with the load, for a live session only.
        if (Client != null)
            _rpOnlineAnchor = Environment.TickCount64;
        dbClient.SetQuery("SELECT `health`,`health_max`,`energy`,`energy_max`,`passive_seconds`," +
                          "`strength_level`,`strength_seconds`,`stamina_level`,`stamina_seconds`," +
                          "`stamina_trained_at`,`stamina_dropped`,`gym_until`,`online_seconds` " +
                          "FROM `user_rp_stats` WHERE `user_id` = @id LIMIT 1");
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
        RpStrengthLevel = Convert.ToInt32(row["strength_level"]);
        RpStrengthSeconds = Convert.ToInt32(row["strength_seconds"]);
        RpStaminaLevel = Convert.ToInt32(row["stamina_level"]);
        RpStaminaSeconds = Convert.ToInt32(row["stamina_seconds"]);
        RpStaminaTrainedAt = Convert.ToInt32(row["stamina_trained_at"]);
        RpStaminaDropped = Convert.ToInt32(row["stamina_dropped"]) != 0;
        RpGymUntil = Convert.ToInt32(row["gym_until"]);
        _rpOnlineBase = Convert.ToInt32(row["online_seconds"]);
        // Stamina IS max health: whatever the row says, the level decides.
        RpHealthMax = Rooms.Gym.GymState.MaxHealthFor(RpStaminaLevel);
        RpHealth = Math.Min(RpHealth, RpHealthMax);
    }

    // Read-and-write of one player's RP stats is one step under this lock, so
    // two saves on different threads (the room tick's queued save on
    // RoomTickWriter, a direct one from :hit or a medkit) cannot interleave -
    // the one that finishes last always carries the newest values. A leaf
    // lock: nothing is taken inside it.
    private readonly object _rpStatsSaveLock = new();

    public void SaveRpStats()
    {
        lock (_rpStatsSaveLock)
        {
            using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
            dbClient.SetQuery("UPDATE `user_rp_stats` SET `health` = @hp, `health_max` = @hpmax, `energy` = @en, `energy_max` = @enmax, `passive_seconds` = @passive, " +
                              "`strength_level` = @strlvl, `strength_seconds` = @strsec, `stamina_level` = @stalvl, `stamina_seconds` = @stasec, " +
                              "`stamina_trained_at` = @statrained, `stamina_dropped` = @stadropped, `gym_until` = @gymuntil, `online_seconds` = @online WHERE `user_id` = @id");
            dbClient.AddParameter("hp", RpHealth);
            dbClient.AddParameter("hpmax", RpHealthMax);
            dbClient.AddParameter("en", RpEnergy);
            dbClient.AddParameter("enmax", RpEnergyMax);
            dbClient.AddParameter("passive", RpPassiveSeconds);
            dbClient.AddParameter("strlvl", RpStrengthLevel);
            dbClient.AddParameter("strsec", RpStrengthSeconds);
            dbClient.AddParameter("stalvl", RpStaminaLevel);
            dbClient.AddParameter("stasec", RpStaminaSeconds);
            dbClient.AddParameter("statrained", RpStaminaTrainedAt);
            dbClient.AddParameter("stadropped", RpStaminaDropped ? 1 : 0);
            dbClient.AddParameter("gymuntil", RpGymUntil);
            dbClient.AddParameter("online", RpOnlineSeconds);
            dbClient.AddParameter("id", Id);
            dbClient.RunQuery();
        }
    }

    // pixelrp UI settings: the player's chosen UI chrome color scheme
    // ("#rrggbb", empty = client default). Lazily loaded like the RP stats;
    // sent to the client at login (RpUiSettingsComposer) and saved when the
    // client picks a scheme (RpSaveUiSettingsEvent).
    public bool RpUiSettingsLoaded { get; set; }
    public string RpUiChromeColor { get; set; } = "";
    // the HUD's surface opacity for a player with no saved settings - the
    // client's DEFAULT_CHROME_OPACITY (UiChrome.ts)
    public int RpUiChromeOpacity { get; set; } = 90;
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

    // pixelrp macros: the player's key/mouse bindings as one JSON document
    // (see 62_Macros.sql for the shape and why it is a blob). Empty string =
    // "no macros saved", which the client reads as its own defaults. Lazily
    // loaded like the UI settings; pushed at login by RpMacrosComposer and
    // written by RpSaveMacrosEvent, which validates before this is ever set.
    public bool RpMacrosLoaded { get; set; }
    public string RpMacros { get; set; } = "";

    public void EnsureRpMacrosLoaded()
    {
        if (RpMacrosLoaded)
            return;
        RpMacrosLoaded = true;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `data` FROM `user_macros` WHERE `user_id` = @id LIMIT 1");
        dbClient.AddParameter("id", Id);
        var row = dbClient.GetRow();
        if (row == null)
            return;
        RpMacros = Convert.ToString(row["data"]) ?? "";
    }

    public void SaveRpMacros()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("REPLACE INTO `user_macros` (`user_id`,`data`) VALUES (@id,@data)");
        dbClient.AddParameter("id", Id);
        dbClient.AddParameter("data", RpMacros);
        dbClient.RunQuery();
    }

    // pixelrp: the player's settings, kept on the account like the macros
    // (279_UserPrefs.sql) - a JSON map from each setting's storage key to its
    // value. Empty string = never saved, and the client then sends up what its
    // browser had. Lazily loaded; pushed at login by RpPrefsComposer and
    // written by RpSavePrefsEvent, which validates before this is ever set.
    public bool RpPrefsLoaded { get; set; }
    public string RpPrefs { get; set; } = "";

    public void EnsureRpPrefsLoaded()
    {
        if (RpPrefsLoaded)
            return;
        RpPrefsLoaded = true;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `data` FROM `user_prefs` WHERE `user_id` = @id LIMIT 1");
        dbClient.AddParameter("id", Id);
        var row = dbClient.GetRow();
        if (row == null)
            return;
        RpPrefs = Convert.ToString(row["data"]) ?? "";
    }

    public void SaveRpPrefs()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("REPLACE INTO `user_prefs` (`user_id`,`data`) VALUES (@id,@data)");
        dbClient.AddParameter("id", Id);
        dbClient.AddParameter("data", RpPrefs);
        dbClient.RunQuery();
    }

    // pixelrp phone: the two documents behind the phone - preferences and the
    // Notification Center's history - as JSON the client owns (91_PhoneState.sql).
    // Same shape as the macros: '' = never saved, lazily loaded, pushed at login
    // by RpPhoneStateComposer, written by RpSavePhoneStateEvent after it has
    // checked the payload is JSON of the right kind.
    public bool RpPhoneLoaded { get; set; }
    public string RpPhonePrefs { get; set; } = "";
    public string RpPhoneNotifications { get; set; } = "";

    public void EnsureRpPhoneLoaded()
    {
        if (RpPhoneLoaded)
            return;
        RpPhoneLoaded = true;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `prefs`, `notifications` FROM `user_phone` WHERE `user_id` = @id LIMIT 1");
        dbClient.AddParameter("id", Id);
        var row = dbClient.GetRow();
        if (row == null)
            return;
        RpPhonePrefs = Convert.ToString(row["prefs"]) ?? "";
        RpPhoneNotifications = Convert.ToString(row["notifications"]) ?? "";
    }

    public void SaveRpPhone()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("REPLACE INTO `user_phone` (`user_id`,`prefs`,`notifications`) VALUES (@id,@prefs,@notifications)");
        dbClient.AddParameter("id", Id);
        dbClient.AddParameter("prefs", RpPhonePrefs);
        dbClient.AddParameter("notifications", RpPhoneNotifications);
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

    // The backpack's logic lives in RpInventoryStore, keyed by user id, so the
    // City Panel can reach an offline player's too. These are the logged-in
    // player's doors onto it.
    public List<(int Slot, string Item, int Count)> LoadRpInventory() => RpInventoryStore.Load(Id);

    // A stack holds at most this many; the next item overflows into a free
    // slot (or fails as backpack-full like any other add).
    public const int RpStackCap = 10;

    /// <summary>
    /// pixelrp: AddRpItem's answer when the item is one a player may only hold
    /// ONE of and they already have it (as opposed to -1, a full backpack).
    /// </summary>
    public const int RpAlreadyHeld = -2;

    /// <summary>
    /// pixelrp: AddRpItem's answer when the database refused the write, so
    /// nothing was added (RpInventoryStore.Add).
    /// </summary>
    public const int RpAddFailed = -3;

    /// <summary>Adds one of an item (stacking onto an existing slot of the
    /// same item, else the first free carry slot). Returns the slot, -1 when
    /// the backpack is full, <see cref="RpAlreadyHeld"/> or
    /// <see cref="RpAddFailed"/>.</summary>
    public int AddRpItem(string item) => RpInventoryStore.Add(Id, item, RpUnlockedSlots);

    /// <summary>
    /// pixelrp: put a new weapon straight into the Weapon slot, which must be
    /// empty. Returns false when it is not. Needs no free carry slot, so a
    /// full backpack can still be handed one to hold.
    /// </summary>
    public bool AddRpItemEquipped(string item) => RpInventoryStore.AddEquipped(Id, item);

    /// <summary>Moves the backpack item in `from` into `to`, swapping when the
    /// target slot is occupied.</summary>
    public void MoveRpItem(int from, int to) => RpInventoryStore.Move(Id, from, to);

    /// <summary>Removes one of whatever sits in the slot. Returns the item
    /// key, or null when the slot is empty.</summary>
    public string ConsumeRpItem(int slot) => RpInventoryStore.Consume(Id, slot);

    /// <summary>pixelrp: the backpack bin - throws away `count` of what sits
    /// in the slot, the whole stack when `count` covers it. Returns the item
    /// key and how many went, or (null, 0) when the slot is empty.</summary>
    public (string Item, int Count) DiscardRpItem(int slot, int count) => RpInventoryStore.Discard(Id, slot, count);

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
            // pixelrp: no credits - see ChangeCredits.
            return
                $"UPDATE `users` SET `online` = false, `last_online` = '{UnixTimestamp.GetNow()}', `activity_points` = '{Duckets}', `vip_points` = '{Diamonds}', `home_room` = '{HomeRoom}', `gotw_points` = '{GotwPoints}', `time_muted` = '{TimeMuted}',`friend_bar_state` = '{FriendBarStateUtility.GetInt(FriendbarState)}' WHERE id = '{Id}' LIMIT 1;UPDATE `user_statistics` SET `roomvisits` = '{HabboStats.RoomVisits}', `onlineTime` = '{OnlineTimeTotal}', `respect` = '{HabboStats.Respect}', `respectGiven` = '{HabboStats.RespectGiven}', `giftsGiven` = '{HabboStats.GiftsGiven}', `giftsReceived` = '{HabboStats.GiftsReceived}', `dailyRespectPoints` = '{HabboStats.DailyRespectPoints}', `dailyPetRespectPoints` = '{HabboStats.DailyPetRespectPoints}', `AchievementScore` = '{HabboStats.AchievementPoints}', `quest_id` = '{HabboStats.QuestId}', `quest_progress` = '{HabboStats.QuestProgress}', `groupid` = '{HabboStats.FavouriteGroupId}',`forum_posts` = '{HabboStats.ForumPosts}' WHERE `id` = '{Id}' LIMIT 1;";
        }
    }

    public bool CacheExpired()
    {
        var span = DateTime.Now - _timeCached;
        return span.TotalMinutes >= 30;
    }

    public bool InitProcess()
    {
        // pixelrp: never once a logout has begun. A login carries on for a
        // moment after its connection drops, and a timer started after the
        // logout has been and gone is never stopped: it ticks for good and
        // keeps this Habbo in memory with it. Under the lock OnDisconnect
        // claims the logout with, so the timer is either started before that
        // claim (and disposed by the logout) or not at all.
        lock (_logoutLock)
        {
            if (_logoutStarted)
                return false;
            Process = new();
            return Process.Init(this);
        }
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
        // The online-time clock for this session (OnlineTimeTotal).
        SessionStart = UnixTimestamp.GetNow();
        //Quests = data.Quests;
        _disconnected = false;
        InitFx();
        InitClothing();
    }


    public event EventHandler? Disconnected;
    /// <summary>
    /// pixelrp: characters whose logout has begun and not yet been saved, for
    /// the login gate (Authenticator.PreviousSessionGone): a character is never
    /// loaded from the database while its last session is still writing to it.
    /// </summary>
    private static readonly ConcurrentDictionary<int, TaskCompletionSource> Leaving = new();

    public static Task LeavingTask(int userId) => Leaving.TryGetValue(userId, out var leaving) ? leaving.Task : Task.CompletedTask;

    public void OnDisconnect()
    {
        if (_disconnected)
            return;
        // pixelrp: claimed under the lock InitProcess takes - see there.
        lock (_logoutLock)
        {
            if (_logoutStarted)
                return;
            _logoutStarted = true;
        }
        var leaving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Leaving[Id] = leaving;

        // pixelrp: every step below runs on its own (LogoutStep). They used to
        // run bare, one after another, so a single throw - a database pool
        // timeout is enough - skipped everything after it: the player was never
        // unregistered, their avatar stayed standing in the room, and the room
        // never unloaded. Now a failing step is logged and the rest still run,
        // and the finally makes sure the player always leaves the hotel.
        try
        {
            LogoutStep(() => Corporations.ShiftManager.InterruptForDisconnect(this));
            // pixelrp: a jam holds your place rather than dropping you. Reloading
            // the client is a disconnect, and a jam you lose by refreshing the page
            // is not a jam anybody can use - so this only marks you away, and the
            // jam's own cycle gives up on you a minute and a half later.
            LogoutStep(() => Jam.JamManager.OnDisconnect(Id));
            // pixelrp: drop this player from any shared note they had open
            LogoutStep(() => Notes.NotesUtility.ClearPresence(Id));
            // pixelrp: and forget which support conversation they were reading -
            // the registry is keyed by player id and outlives the session.
            LogoutStep(() => Support.SupportUtility.Forget(Id));
            // pixelrp offers: and any sale they were half way through.
            LogoutStep(() => Rooms.Offers.OfferState.Forget(Id));
            // pixelrp trading: and any trade - nothing changes hands.
            LogoutStep(() => Rooms.Trading.RpTrades.Forget(Id, true));
            // pixelrp banking: and the ATM they were standing at - that is
            // session state too, and nothing else ever took the entry out.
            LogoutStep(() => Banking.AtmSessions.End(Id));
            // pixelrp marketplace: an open market is session state the same way.
            LogoutStep(() => Banking.RpMarket.Forget(Id));
            // pixelrp gym: the online clock stops here, and the training done
            // since the last save is kept.
            LogoutStep(() => Rooms.Gym.GymState.OnLogout(this));
            // pixelrp police: logging out in handcuffs is an arrest - charged with
            // Logout and booked, the cuffs back to their officer. First, while the
            // cuff is still on record: Forget below is what takes it off.
            LogoutStep(() => Plus.HabboHotel.Rooms.Chat.Commands.User.Police.ArrestCommand.OnCuffedLogout(this));
            // pixelrp police: a cuff, a stun and an escort are all session state, and
            // reloading the client ends the session - so the cuffs come off with it.
            // Done here rather than left to the room-leave path below because that
            // path is conditional (Dispose only runs it while CurrentRoom is set) and
            // the registry is not: it is keyed by player id and lives as long as the
            // emulator, so anything missed follows the player into their next session.
            // CurrentRoom is still set at this point, which is what lets the escort
            // unpair properly; a session that has already lost its room clears anyway.
            LogoutStep(() => Plus.HabboHotel.Rooms.Chat.Commands.User.Police.PoliceState.Forget(CurrentRoom, Id));
            // pixelrp jury trials: a prisoner logging out once court has started
            // calls it off; a juror gives up their vote. Before the jail's own
            // step, while their sentence is still in memory.
            LogoutStep(() => Plus.HabboHotel.Rooms.Chat.Commands.User.Police.JuryTrials.OnLogout(Id));
            // pixelrp jail: a sentence is not session state - it waits for them.
            // Only its clock stops, saved where it is.
            LogoutStep(() => Plus.HabboHotel.Rooms.Chat.Commands.User.Police.JailState.OnLogout(Id));

            LogoutStep(() => Disconnected?.Invoke(this, EventArgs.Empty));
        }
        finally
        {
            try
            {
                if (Process != null)
                    Process.Dispose();
            }
            catch { }
            _disconnected = true;
            LogoutStep(() => PlusEnvironment.Game.ClientManager.UnregisterClient(Id, Username, Client));
            if (!_habboSaved)
            {
                _habboSaved = true;
                LogoutStep(() =>
                {
                    using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
                    // pixelrp: no credits - every change was written as it
                    // happened (ChangeCredits), and a total written now could
                    // only be older than one already in.
                    dbClient.RunQuery(
                        $"UPDATE `users` SET `online` = false, `last_online` = '{(int)UnixTimestamp.GetNow()}', `activity_points` = '{Duckets}', `vip_points` = '{Diamonds}', `home_room` = '{HomeRoom}', `gotw_points` = '{GotwPoints}', `time_muted` = '{TimeMuted}',`friend_bar_state` = '{FriendBarStateUtility.GetInt(FriendbarState)}', `bubble_id` = '{CustomBubbleId}' WHERE id = '{Id}' LIMIT 1;UPDATE `user_statistics` SET `roomvisits` = '{HabboStats.RoomVisits}', `onlineTime` = '{OnlineTimeTotal}', `respect` = '{HabboStats.Respect}', `respectGiven` = '{HabboStats.RespectGiven}', `giftsGiven` = '{HabboStats.GiftsGiven}', `giftsReceived` = '{HabboStats.GiftsReceived}', `dailyRespectPoints` = '{HabboStats.DailyRespectPoints}', `dailyPetRespectPoints` = '{HabboStats.DailyPetRespectPoints}', `AchievementScore` = '{HabboStats.AchievementPoints}', `quest_id` = '{HabboStats.QuestId}', `quest_progress` = '{HabboStats.QuestProgress}', `groupid` = '{HabboStats.FavouriteGroupId}',`forum_posts` = '{HabboStats.ForumPosts}' WHERE `id` = '{Id}' LIMIT 1;");
                    if (Permissions.HasRight("mod_tickets"))
                        dbClient.RunQuery($"UPDATE `moderation_tickets` SET `status` = 'open', `moderator_id` = '0' WHERE `status` ='picked' AND `moderator_id` = '{Id}'");
                });
            }
            // pixelrp: saved - a login of this character may load it now.
            Leaving.TryRemove(new KeyValuePair<int, TaskCompletionSource>(Id, leaving));
            leaving.TrySetResult();
            // pixelrp: the Wanted list only carries players who are HERE, so
            // leaving has to be announced or the rest of the hotel keeps a row for
            // somebody who has gone - and with the clock frozen it would sit there
            // at a fixed number forever rather than counting itself off. Only
            // when they were on it, though: nobody else's list changes otherwise.
            //
            // AFTER UnregisterClient above, deliberately: the broadcast rebuilds
            // the list from who is registered, so running it any earlier would
            // send one that still had this player on it.
            LogoutStep(() => Corporations.WantedUtility.BroadcastIfCharged(Id));
            // pixelrp discord sync: drop the Online role shortly after logout.
            LogoutStep(() => Plus.HabboHotel.Discord.DiscordSyncUtility.Enqueue(Id, "logout"));
            LogoutStep(() => Dispose());
            // pixelrp hospital: a player who logs out while knocked out starts
            // the clock again when they come back (HospitalAdmission's own
            // rule) - but the old entry stayed, so the first tick after their
            // return admitted them at once. After Dispose, which takes them
            // out of the room, so its tick has nobody left to start one for.
            LogoutStep(() => Rooms.HospitalAdmission.Forget(Id));
            Client = null;
        }
    }

    /// <summary>
    /// pixelrp: one step of <see cref="OnDisconnect"/>. A step that throws is
    /// logged, and the logout carries on with the next one.
    /// </summary>
    private static void LogoutStep(Action step)
    {
        try
        {
            step();
        }
        catch (Exception e)
        {
            Plus.Core.ExceptionLogger.LogException(e);
        }
    }

    public void Dispose()
    {
        // pixelrp: the room's user manager is null once the room has been
        // unloaded with this player still inside it (Room.Dispose - :unload,
        // a crashed room). There is nobody left to remove them from then, and
        // calling through it threw out of the logout.
        if (InRoom && CurrentRoom != null)
            CurrentRoom.GetRoomUserManager()?.RemoveUserFromRoom(Client, false);
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
                ChangeCredits(creditUpdate);
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

    /// <param name="lockOldRoom">
    /// pixelrp: take the old room's _cycleLock while leaving it - for a room
    /// change run off that room's own threads (RoomTransfers), so the removal
    /// cannot interleave with the room's tick or its movement frames, as it
    /// could not when those trips ran on them.
    /// </param>
    public void PrepareRoom(uint id, string password, bool lockOldRoom = false)
    {
        if (Client == null || Client.GetHabbo() == null)
            return;

        // pixelrp jury trials: the jurors and the prisoner stay in the
        // Courthouse while court is in session (JuryTrials) - short of logging
        // out. Silently (Twist, 2026-10-20): they will be stood where they
        // cannot walk, so this only catches a way out that is not on foot.
        // Before the jail's door, which would send the prisoner back to their
        // cell.
        if (Plus.HabboHotel.Rooms.Chat.Commands.User.Police.JuryTrials.HeldInCourt(Id, id))
        {
            EndTeleport();
            return;
        }

        // pixelrp jail: a prisoner goes nowhere but the jail. Every way out of
        // a room ends up here - an arrow, a teleporter, :follow, an escort, the
        // login forward - so this one check is the cell door. The exception is
        // a staff :summon, which lets them into that one room (JailState.
        // AllowSummon). In the jail already, they are simply told; anywhere
        // else (the room they were summoned to, a relog that forwarded them
        // somewhere else, a jail that has moved) they are sent back, onto a
        // bed. The teleport an arrow or teleporter started is called off either
        // way, or its flags would outlive the refusal.
        if (!Plus.HabboHotel.Rooms.Chat.Commands.User.Police.JailState.MayEnter(Id, id, out var jailRoomId))
        {
            EndTeleport();
            if (InRoom && CurrentRoom != null && Plus.HabboHotel.Rooms.Chat.Commands.User.Police.JailState.IsJailRoom(CurrentRoom.Id))
            {
                Client.SendWhisper("You are in jail.");
                return;
            }
            Plus.HabboHotel.Rooms.Chat.Commands.User.Police.JailState.SendToJail(this, jailRoomId, forward: true);
            return;
        }
        // And the one door that opens for them whatever the room's settings:
        // a full or locked jail still takes a prisoner in.
        var jailEntry = Plus.HabboHotel.Rooms.Chat.Commands.User.Police.JailState.IsJailed(Id);
        // pixelrp :backup: an officer answering a backup request gets into the
        // requesting officer's room the same way. Spent on this entry, so it
        // never outlives the one trip it was for.
        var backupEntry = (BackupEntryRoomId != 0) && (BackupEntryRoomId == id);
        BackupEntryRoomId = 0;

        // AUTHORISE THE ROOM THE SERVER IS SENDING THEM TO, or the client's own
        // answer to the RoomReadyComposer below is thrown away as an injected
        // packet and they never arrive.
        //
        // OpenFlatConnectionEvent lets a non-staff client enter only the room
        // it was forwarded to, the room it is already in, or the one it is
        // teleporting to. A server-initiated PrepareRoom - an escort summoning
        // its captive, :summon, anything that moves somebody who did not ask -
        // matched none of those: it clears CurrentRoom on the way past, and
        // nothing had set AuthorizedRoomEntryId because SendRoomForward, which
        // is what normally sets it, was not the path taken. So the player sat
        // in the room they started in while the server believed it had moved
        // them. STAFF WERE EXEMPT FROM THE GATE, which is why this only ever
        // showed up with an ordinary player on the other end.
        //
        // Safe here: an injected entry is refused by that gate BEFORE it can
        // reach this method, so the only callers that get to set this are the
        // server itself and a client whose entry was already allowed.
        Client.GetHabbo().AuthorizedRoomEntryId = id;

        if (Client.GetHabbo().InRoom)
        {
            var oldRoom = Client.GetHabbo().CurrentRoom;
            // A disposed room (unloaded while we were still in it, e.g. a floor
            // plan save) has no user manager left; clear the stale reference or
            // every future room entry NREs and the client stays on a black screen.
            if (oldRoom != null && !oldRoom.MDisposed && lockOldRoom)
                oldRoom.GetRoomUserManager().RemoveUserFromRoomLocked(Client);
            else if (oldRoom != null && !oldRoom.MDisposed)
                oldRoom.GetRoomUserManager().RemoveUserFromRoom(Client, false);
            else
                Client.GetHabbo().CurrentRoom = null;
        }
        // An entry for any room but the teleport's own means the teleport is
        // over - superseded by a summon, an escort, the navigator. It used to
        // eject the player to the hotel view instead, which with the flag
        // never cleared meant every room change after a single teleport. Every
        // entry that gets this far was already chosen by the server or passed
        // the injection gate, so there is nothing here left to refuse.
        if (Client.GetHabbo().IsTeleporting && Client.GetHabbo().TeleportingRoomId != id)
            Client.GetHabbo().EndTeleport();
        if (!PlusEnvironment.Game.RoomManager.TryLoadRoom(id, out var room))
        {
            Eject();
            return;
        }
        if (room.IsCrashed)
        {
            Client.SendPopup("This room has crashed! :(");
            Eject();
            return;
        }
        if (room.GetRoomUserManager().UserCount >= room.UsersMax && !Client.GetHabbo().Permissions.HasRight("room_enter_full") && Client.GetHabbo().Id != room.OwnerId && !jailEntry && !backupEntry)
        {
            Client.Send(new CantConnectComposer(1));
            Eject();
            return;
        }
        if (!Permissions.HasRight("room_ban_override") && room.GetBans().IsBanned(Id))
        {
            RoomAuthOk = false;
            Client.GetHabbo().RoomAuthOk = false;
            Client.Send(new CantConnectComposer(4));
            Eject();
            return;
        }
        Client.Send(new OpenConnectionComposer());
        if (!room.CheckRights(Client, true, true) && !Client.GetHabbo().IsTeleporting && !Client.GetHabbo().IsHopping && !jailEntry && !backupEntry)
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
                Eject();
                return;
            }
            if (room.Access == RoomAccess.Password && !Client.GetHabbo().Permissions.HasRight("room_enter_locked"))
            {
                if (password.ToLower() != room.Password.ToLower() || string.IsNullOrWhiteSpace(password))
                {
                    Client.Send(new GenericErrorComposer(-100002));
                    Eject();
                    return;
                }
            }
        }
        if (!EnterRoom(room))
            Eject();

        // The hotel view, and the end of any teleport that was bringing them
        // here: it did not arrive, so nothing else will clear it.
        void Eject()
        {
            EndTeleport();
            Client.Send(new CloseConnectionComposer());
        }
    }

    public bool EnterRoom(Room room)
    {
        if (room == null)
            return false;
        Client.GetHabbo().CurrentRoom = room;
        // pixelrp: marked BEFORE RoomReady goes out - the client asks for the
        // room the moment RoomReady reaches it, and that request must find the
        // room already marked as sent (RoomEntry, below), however fast it is.
        EntrySentRoomId = room.RoomId;
        // pixelrp: RoomReady and the whole room behind it go as ONE message
        // (GameClient.BeginBatch): the room arrives complete, in one piece.
        Client.BeginBatch();
        try
        {
            SendRoom(room);
        }
        finally
        {
            Client.EndBatch();
        }
        return true;
    }

    private void SendRoom(Room room)
    {
        Client.Send(new RoomReadyComposer(room.RoomId, room.ModelName));
        if (room.Wallpaper != "0.0")
            Client.Send(new RoomPropertyComposer("wallpaper", room.Wallpaper));
        if (room.Floor != "0.0")
            Client.Send(new RoomPropertyComposer("floor", room.Floor));
        Client.Send(new RoomPropertyComposer("landscape", room.Landscape));
        Client.Send(new RoomRatingComposer(room.Score, !(Client.GetHabbo().RatedRooms.Contains(room.RoomId) || room.OwnerId == Client.GetHabbo().Id)));
        // pixelrp: queued, not written here (RoomTickWriter's ordered queue, so
        // the visit is opened before anything closes it).
        RoomTickWriter.QueueVisitEntry(Client.GetHabbo().Id, room.RoomId);

        if (room.OwnerId != Id)
        {
            Client.GetHabbo().HabboStats.RoomVisits += 1;
            PlusEnvironment.Game.AchievementManager.ProgressAchievement(Client, "ACH_RoomEntry", 1);
        }

        // pixelrp: the room itself - its shape, everybody in it, its furni -
        // straight behind RoomReady, instead of a round trip later when the
        // client asks for it (RoomEntry). If it fails part way, the mark comes
        // off so the client's own request gets the room after all.
        try
        {
            RoomEntry.Send(Client, room, PlusEnvironment.Game.QuestManager);
        }
        catch (Exception e)
        {
            EntrySentRoomId = 0;
            Core.ExceptionLogger.LogException(e);
        }
    }
}