using System.Data;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Data.Moodlight;
using Plus.HabboHotel.Items.Data.Toner;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Speech;
using Plus.HabboHotel.Rooms.Games;
using Plus.HabboHotel.Rooms.Games.Banzai;
using Plus.HabboHotel.Rooms.Games.Football;
using Plus.HabboHotel.Rooms.Games.Freeze;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Rooms.Jukebox;
using Plus.Utilities;
using NLog;
using System.Diagnostics;

namespace Plus.HabboHotel.Rooms;

public class Room : RoomData
{
    private static readonly NLog.ILogger Log = LogManager.GetLogger("Plus.HabboHotel.Rooms.Room");

    private readonly BansComponent _bansComponent;

    private readonly FilterComponent _filterComponent;

    private readonly Dictionary<uint, List<RoomUser>> _tents;
    private readonly TradingComponent _tradingComponent;
    private readonly WiredComponent _wiredComponent;
    private BattleBanzai _banzai;
    private Freeze _freeze;
    private GameItemHandler _gameItemHandler;
    private GameManager _gameManager;

    private Gamemap _gamemap;
    private RoomItemHandling _roomItemHandling;
    private RoomJukeboxManager _jukeboxManager;

    private RoomUserManager _roomUserManager;
    private Soccer _soccer;

    public bool IsCrashed;
    public DateTime LastRegeneration;
    public DateTime LastTimerReset;
    public bool MDisposed;
    public MoodlightData MoodlightData;

    public Dictionary<int, double> MutedUsers;

    public Task ProcessTask;
    // Set by RoomManager when ProcessTask starts, so a skipped tick can report
    // how long the still-running ProcessRoom has been going.
    public DateTime ProcessTaskStarted;
    public bool RoomMuted;

    public TeamManager Teambanzai;
    public TeamManager Teamfreeze;

    public TonerData TonerData;

    public List<int> UsersWithRights;

    public Room(RoomData data)
        : base(data)
    {
        IsLagging = 0;
        Unloaded = false;
        IdleTime = 0;
        RoomMuted = false;
        MutedUsers = new();
        _tents = new();
        _gamemap = new(this, data.Model);
        _roomItemHandling = new(this);
        _roomUserManager = new(this);
        // Eager, not lazy: ??= is not thread safe, and the room cycle and a
        // packet handler can both reach this. Harmless while the manager was a
        // pass-through to a static station - it now holds this room's queue, so
        // losing that race would mean two managers and one queue vanishing.
        _jukeboxManager = new(this);
        _filterComponent = new(this);
        _wiredComponent = new(this);
        _bansComponent = new(this);
        _tradingComponent = new(this);
        GetRoomItemHandler().LoadFurniture();
        GetGameMap().GenerateMaps();
        LoadRights();
        LoadFilter();
        InitBots();
        InitPets();
        LastRegeneration = DateTime.Now;
    }

    public int IsLagging { get; set; }
    public bool Unloaded { get; set; }
    public int IdleTime { get; set; }

    public List<string> WordFilterList { get; set; }

    public int UserCount => _roomUserManager.GetRoomUsers().Count;

    // pixelrp: when RoomManager.TryLoadRoom last handed this room to somebody
    // (monotonic ms) - the idle unload leaves a room alone for a while after.
    private long _handedOutMs;

    public void MarkHandedOut() => Interlocked.Exchange(ref _handedOutMs, Environment.TickCount64);

    public bool HandedOutWithin(long ms) => Environment.TickCount64 - Interlocked.Read(ref _handedOutMs) < ms;

    public uint RoomId => Id;

    public bool CanTradeInRoom => true;

    public Gamemap GetGameMap() => _gamemap;

    public RoomItemHandling GetRoomItemHandler()
    {
        if (_roomItemHandling == null) _roomItemHandling = new(this);
        return _roomItemHandling;
    }

    public RoomUserManager GetRoomUserManager() => _roomUserManager;

    public RoomJukeboxManager GetJukeboxManager() => _jukeboxManager ??= new RoomJukeboxManager(this);


    public Soccer GetSoccer()
    {
        if (_soccer == null)
            _soccer = new(this);
        return _soccer;
    }

    public TeamManager GetTeamManagerForBanzai()
    {
        if (Teambanzai == null)
            Teambanzai = TeamManager.CreateTeam("banzai");
        return Teambanzai;
    }

    public TeamManager GetTeamManagerForFreeze()
    {
        if (Teamfreeze == null)
            Teamfreeze = TeamManager.CreateTeam("freeze");
        return Teamfreeze;
    }

    public BattleBanzai GetBanzai()
    {
        if (_banzai == null)
            _banzai = new(this);
        return _banzai;
    }

    public Freeze GetFreeze()
    {
        if (_freeze == null)
            _freeze = new(this);
        return _freeze;
    }

    public GameManager GetGameManager()
    {
        if (_gameManager == null)
            _gameManager = new(this);
        return _gameManager;
    }

    public GameItemHandler GetGameItemHandler()
    {
        if (_gameItemHandler == null)
            _gameItemHandler = new(this);
        return _gameItemHandler;
    }

    public bool GotSoccer() => _soccer != null;

    public bool GotBanzai() => _banzai != null;

    public bool GotFreeze() => _freeze != null;

    public void ClearTags()
    {
        Tags.Clear();
    }

    public void AddTagRange(List<string> tags)
    {
        Tags.AddRange(tags);
    }

    public void InitBots()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            $"SELECT `id`,`room_id`,`name`,`motto`,`look`,`x`,`y`,`z`,`rotation`,`gender`,`user_id`,`ai_type`,`walk_mode`,`automatic_chat`,`speaking_interval`,`mix_sentences`,`chat_bubble` FROM `bots` WHERE `room_id` = '{RoomId}' AND `ai_type` != 'pet'");
        var data = dbClient.GetTable();
        if (data == null)
            return;
        // pixelrp: every bot's lines in ONE query, not one per bot - a room load
        // waited on each in turn. Ordered as each bot's own query read them.
        var speechByBot = new Dictionary<int, List<RandomSpeech>>();
        if (data.Rows.Count > 0)
        {
            var botIds = string.Join(",", data.Rows.Cast<DataRow>().Select(bot => Convert.ToInt32(bot["id"])));
            dbClient.SetQuery($"SELECT `bot_id`,`text` FROM `bots_speech` WHERE `bot_id` IN ({botIds}) ORDER BY `bot_id`,`id`");
            var allSpeech = dbClient.GetTable();
            if (allSpeech != null)
            {
                foreach (DataRow speech in allSpeech.Rows)
                {
                    var botId = Convert.ToInt32(speech["bot_id"]);
                    if (!speechByBot.TryGetValue(botId, out var lines))
                        speechByBot[botId] = lines = new List<RandomSpeech>();
                    lines.Add(new(Convert.ToString(speech["text"]), botId));
                }
            }
        }
        foreach (DataRow bot in data.Rows)
        {
            var speeches = speechByBot.TryGetValue(Convert.ToInt32(bot["id"]), out var botLines) ? botLines : new List<RandomSpeech>();
            _roomUserManager.DeployBot(
                new(Convert.ToInt32(bot["id"]), Convert.ToUInt32(bot["room_id"]), Convert.ToString(bot["ai_type"]), Convert.ToString(bot["walk_mode"]), Convert.ToString(bot["name"]),
                    Convert.ToString(bot["motto"]), Convert.ToString(bot["look"]), int.Parse(bot["x"].ToString()), int.Parse(bot["y"].ToString()), int.Parse(bot["z"].ToString()),
                    int.Parse(bot["rotation"].ToString()), 0, 0, 0, 0, ref speeches, "M", 0, Convert.ToInt32(bot["user_id"].ToString()), Convert.ToBoolean(bot["automatic_chat"]),
                    Convert.ToInt32(bot["speaking_interval"]), ConvertExtensions.EnumToBool(bot["mix_sentences"].ToString()), Convert.ToInt32(bot["chat_bubble"])), null);
        }
    }

    public void InitPets()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        // pixelrp: each pet with its pet data in ONE query, not one more per pet.
        // The inner join leaves out a pet with no bots_petdata row, which the
        // per-pet lookup skipped too.
        dbClient.SetQuery(
            "SELECT b.`id`,b.`user_id`,b.`room_id`,b.`name`,b.`x`,b.`y`,b.`z`," +
            "p.`type`,p.`race`,p.`color`,p.`experience`,p.`energy`,p.`nutrition`,p.`respect`,p.`createstamp`,p.`have_saddle`,p.`anyone_ride`,p.`hairdye`,p.`pethair`,p.`gnome_clothing` " +
            $"FROM `bots` b INNER JOIN `bots_petdata` p ON p.`id` = b.`id` WHERE b.`room_id` = '{RoomId}' AND b.`ai_type` = 'pet'");
        var data = dbClient.GetTable();
        if (data == null)
            return;
        foreach (DataRow row in data.Rows)
        {
            var pet = new Pet(Convert.ToInt32(row["id"]), Convert.ToInt32(row["user_id"]), Convert.ToUInt32(row["room_id"]), Convert.ToString(row["name"]), Convert.ToInt32(row["type"]),
                Convert.ToString(row["race"]),
                Convert.ToString(row["color"]), Convert.ToInt32(row["experience"]), Convert.ToInt32(row["energy"]), Convert.ToInt32(row["nutrition"]), Convert.ToInt32(row["respect"]),
                Convert.ToDouble(row["createstamp"]), Convert.ToInt32(row["x"]), Convert.ToInt32(row["y"]),
                Convert.ToDouble(row["z"]), Convert.ToInt32(row["have_saddle"]), Convert.ToInt32(row["anyone_ride"]), Convert.ToInt32(row["hairdye"]), Convert.ToInt32(row["pethair"]),
                Convert.ToString(row["gnome_clothing"]));
            var rndSpeechList = new List<RandomSpeech>();
            _roomUserManager.DeployBot(
                new(pet.PetId, RoomId, "pet", "freeroam", pet.Name, "", pet.Look, pet.X, pet.Y, Convert.ToInt32(pet.Z), 0, 0, 0, 0, 0, ref rndSpeechList, "", 0, pet.OwnerId, false, 0, false,
                    0), pet);
        }
    }

    public FilterComponent GetFilter() => _filterComponent;

    public WiredComponent GetWired() => _wiredComponent;

    public BansComponent GetBans() => _bansComponent;

    public TradingComponent GetTrading() => _tradingComponent;

    public void LoadRights()
    {
        UsersWithRights = new();
        if (Group != null)
            return;
        DataTable data = null;
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT room_rights.user_id FROM room_rights WHERE room_id = @roomid");
            dbClient.AddParameter("roomid", Id);
            data = dbClient.GetTable();
        }
        if (data != null)
            foreach (DataRow row in data.Rows)
                UsersWithRights.Add(Convert.ToInt32(row["user_id"]));
    }

    private void LoadFilter()
    {
        WordFilterList = new();
        DataTable data = null;
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT * FROM `room_filter` WHERE `room_id` = @roomid;");
            dbClient.AddParameter("roomid", Id);
            data = dbClient.GetTable();
        }
        if (data == null)
            return;
        foreach (DataRow row in data.Rows) WordFilterList.Add(Convert.ToString(row["word"]));
    }

    public bool CheckRights(GameClient session) => CheckRights(session, false);

    /// <summary>
    /// pixelrp: a global-rights permission is a LICENCE, not a grant.
    ///
    /// `room_any_owner` and `room_any_rights` used to answer every hour of the
    /// day, so a staff member's ordinary mis-click could toggle a furni's
    /// state, drag a piece off its tile, or eject somebody else's furni to
    /// their inventory. None of that was a moderation action anybody chose; it
    /// was the cost of having the tools armed permanently.
    ///
    /// They answer now only while their holder is clocked in at City
    /// Government. The tools are armed for the shift and disarmed for the rest
    /// of the day.
    ///
    /// IsStaffOnDuty is a dictionary lookup over in-memory shift sessions, not
    /// a query - which matters, because this runs on every click and every
    /// room entry. It must not become a database read.
    /// </summary>
    private static bool HoldsGlobalRight(GameClient session, string right) =>
        session.GetHabbo().Permissions.HasRight(right) &&
        Corporations.ShiftManager.IsStaffOnDuty(session.GetHabbo().Id);

    /// <summary>The permission that lets a player set a room ad's image and offsets.</summary>
    public const string BrandingRight = "room_item_save_branding_items";

    /// <summary>
    /// pixelrp: may this player edit the room ads (ads_* branding furni) here?
    ///
    /// Two ways in, both needing <see cref="BrandingRight"/>: owner rights in
    /// this room, as stock Plus had it, or being clocked in at City Government
    /// - which lets every rank that holds the permission edit ads in any room
    /// WITHOUT the rest of `room_any_owner` (moving, picking up, ejecting other
    /// people's furni). The same licence rule as HoldsGlobalRight: armed for
    /// the shift, disarmed for the rest of the day.
    /// </summary>
    public bool CanEditBranding(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || !habbo.Permissions.HasRight(BrandingRight))
            return false;
        return CheckRights(session, true) || Corporations.ShiftManager.IsStaffOnDuty(habbo.Id);
    }

    /// <summary>The lowest rank that may manage room settings (CanManageSettings).</summary>
    public const int SettingsStaffRank = 5;

    /// <summary>
    /// pixelrp: may this player read or change the room's settings - the
    /// Room tool's name, door, trading, chat, pets, walls, zone type and
    /// emergency access - delete the room, or say who has rights in it?
    /// Staff only: rank 5 and up (the project's staff floor, as for
    /// :roomsettings and the HQ settings), on duty or off. A rank, not the
    /// `room_any_owner` permission, because that one is not granted to every
    /// staff rank. Owning the room is not enough; room settings are a staff job.
    /// </summary>
    public bool CanManageSettings(GameClient session) =>
        session?.GetHabbo() != null && session.GetHabbo().Rank >= SettingsStaffRank;

    /// <summary>The lowest rank that may dress any mannequin (CanDressMannequin).</summary>
    public const int MannequinStaffRank = 2;

    /// <summary>
    /// pixelrp: may this player save a mannequin's outfit or name here?
    /// The room owner, as stock Plus had it, or any staff - every staff rank,
    /// Builder (2) and up - Builders redecorate (migration 285) - on duty or
    /// off (Twist, 2026-10-20). Owner-or-room_any_owner alone meant staff
    /// could not dress a mannequin in a room they did not own unless they
    /// were clocked in. FurnitureMannequinView offers the Save button on the
    /// same rank.
    /// </summary>
    public bool CanDressMannequin(GameClient session) =>
        CheckRights(session, true) || (session?.GetHabbo() != null && session.GetHabbo().Rank >= MannequinStaffRank);

    /// <summary>The lowest rank that may save any room's floor plan (CanEditFloorplan).</summary>
    public const int FloorplanStaffRank = 2;

    /// <summary>
    /// pixelrp: may this player save a new floor plan for this room?
    /// The room owner, as stock Plus had it, or any staff - every rank from
    /// Builder (2) up - in any room, on duty or off (Twist, 2026-10-21).
    /// :floorplan and :floor open the editor from the same rank (migration 286).
    /// </summary>
    public bool CanEditFloorplan(GameClient session) =>
        CheckRights(session, true) || (session?.GetHabbo() != null && session.GetHabbo().Rank >= FloorplanStaffRank);

    public bool CheckRights(GameClient session, bool requireOwnership, bool checkForGroups = false)
    {
        try
        {
            if (session == null || session.GetHabbo() == null)
                return false;
            if (session.GetHabbo().Username == OwnerName && Type == "private")
                return true;
            // Ownership above and granted rights below are untouched: a staff
            // member who owns this room, or was given rights in it, keeps them
            // off duty like anybody else.
            if (HoldsGlobalRight(session, "room_any_owner"))
                return true;
            if (!requireOwnership && Type == "private")
            {
                if (HoldsGlobalRight(session, "room_any_rights"))
                    return true;
                if (UsersWithRights.Contains(session.GetHabbo().Id))
                    return true;
            }
            if (checkForGroups && Type == "private")
            {
                if (Group == null)
                    return false;
                if (Group.IsAdmin(session.GetHabbo().Id))
                    return true;
                if (Group.AdminOnlyDeco == 0)
                {
                    if (Group.IsAdmin(session.GetHabbo().Id))
                        return true;
                }
            }
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
        return false;
    }

    public void OnUserShoot(RoomUser user, Item ball)
    {
        Func<Item, bool> predicate = null;
        string key = null;
        foreach (var item in GetRoomItemHandler().GetFurniObjects(ball.GetX, ball.GetY).ToList())
        {
            if (item.Definition.ItemName.StartsWith("fball_goal_"))
            {
                key = item.Definition.ItemName.Split(new[] { '_' })[2];
                user.UnIdle();
                user.StopDancing();
                PlusEnvironment.Game.AchievementManager.ProgressAchievement(user.GetClient(), "ACH_FootballGoalScored", 1);
                SendPacket(new ActionComposer(user.VirtualId, 1));
            }
        }
        if (key != null)
        {
            if (predicate == null) predicate = p => p.Definition.ItemName == $"fball_score_{key}";
            foreach (var item2 in GetRoomItemHandler().GetFloor.Where(predicate).ToList())
            {
                if (item2.Definition.ItemName == $"fball_score_{key}")
                {
                    if (!string.IsNullOrEmpty(item2.LegacyDataString))
                        item2.LegacyDataString = (Convert.ToInt32(item2.LegacyDataString) + 1).ToString();
                    else
                        item2.LegacyDataString = "1";
                    item2.UpdateState();
                }
            }
        }
    }

    /// <summary>
    /// pixelrp: how many 500ms ticks a room with nobody in it stays loaded -
    /// five minutes (was 60, half a minute). Arrows mostly lead into rooms
    /// someone was in a little while ago; a room still loaded is entered from
    /// memory, a cold one costs a database load of every item, bot and pet
    /// first. Key rooms (KeyRooms) are never unloaded for being empty.
    /// </summary>
    private const int IdleUnloadTicks = 600;

    /// <summary>
    /// pixelrp: how many 500ms ticks a room with nobody in it keeps running -
    /// half a minute, which is where it used to be unloaded. After that it is
    /// quiet until somebody comes back: no rollers, wired, bots or pets, nothing
    /// a room does on its own - just as when it was unloaded at this point, only
    /// still in memory to walk back into. So keeping rooms loaded longer costs
    /// memory, not ticking.
    /// </summary>
    private const int QuietAfterTicks = 60;

    public void ProcessRoom()
    {
        if (IsCrashed || MDisposed)
            return;
        try
        {
            if (GetRoomUserManager().GetRoomUsers().Count == 0)
                IdleTime++;
            else if (IdleTime > 0)
                IdleTime = 0;
            // Asked as the room reaches the unload time, then again every time
            // as long again: a key room left loaded counts on past it, and is
            // not asked (or its furni searched for an arrow) every tick it
            // stays empty - but one that stops being a key room while empty
            // (its corporation gone, its arrow picked up, given away by the
            // PixelRP account) still unloads, within five minutes.
            // pixelrp: UnloadIdleRoom, not UnloadRoom: it stays loaded for
            // anyone handed it on their way in.
            if (IdleTime >= IdleUnloadTicks && (IdleTime % IdleUnloadTicks) == 0 && !KeyRooms.StaysLoaded(this)
                && PlusEnvironment.Game.RoomManager.UnloadIdleRoom(this))
                return;
            if (IdleTime >= QuietAfterTicks)
            {
                // Going quiet is where it used to be unloaded, so it is where
                // it is saved: what Dispose writes (furni moved or switched,
                // pets), once, now.
                if (IdleTime == QuietAfterTicks)
                    SaveQuietChanges();
                return;
            }
            // TEMP stall telemetry (2026-08-25): a phase that overruns the
            // 500ms tick makes RoomManager skip the next tick — the
            // freeze-then-teleport players report. Log which phase ate the
            // time; remove with the [gap] client logger once the stall source
            // is confirmed.
            var sw = Stopwatch.StartNew();
            long tItems = 0, tUsers = 0, tSerialize = 0, tGameItems = 0;
            try
            {
                GetRoomItemHandler().OnCycle();
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
            tItems = sw.ElapsedMilliseconds;
            try
            {
                GetRoomUserManager().OnCycle();
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
            tUsers = sw.ElapsedMilliseconds;
            try
            {
                GetRoomUserManager().SerializeStatusUpdates();
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
            tSerialize = sw.ElapsedMilliseconds;
            try
            {
                if (_gameItemHandler != null)
                    _gameItemHandler.OnCycle();
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
            tGameItems = sw.ElapsedMilliseconds;
            try
            {
                GetWired().OnCycle();
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
            try
            {
                GetJukeboxManager().Cycle();
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
            // pixelrp turfs: a claim in progress here (TurfManager). One probe
            // on an empty dictionary for every room without one.
            try
            {
                Gangs.TurfManager.Tick(this);
            }
            catch (Exception e)
            {
                ExceptionLogger.LogException(e);
            }
            var total = sw.ElapsedMilliseconds;
            if (total > 250)
                Log.Warn($"[stall] Room {Id} ProcessRoom took {total}ms (items={tItems}ms users={tUsers - tItems}ms serialize={tSerialize - tUsers}ms gameitems={tGameItems - tSerialize}ms wired={total - tGameItems}ms)");
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
            OnRoomCrash(e);
        }
    }

    private void SaveQuietChanges()
    {
        try
        {
            GetRoomItemHandler()?.SaveChanges();
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
        try
        {
            GetRoomUserManager()?.UpdatePets();
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    private void OnRoomCrash(Exception e)
    {
        try
        {
            foreach (var user in _roomUserManager.GetRoomUsers().ToList())
            {
                if (user == null || user.GetClient() == null)
                    continue;
                user.GetClient().SendPopup("Sorry, it appears that room has crashed!"); //Unhandled exception in room: " + e);
                try
                {
                    GetRoomUserManager().RemoveUserFromRoom(user.GetClient(), true);
                }
                catch (Exception e2)
                {
                    ExceptionLogger.LogException(e2);
                }
            }
        }
        catch (Exception e3)
        {
            ExceptionLogger.LogException(e3);
        }
        IsCrashed = true;
        PlusEnvironment.Game.RoomManager.UnloadRoom(Id);
    }


    public bool CheckMute(GameClient session)
    {
        if (MutedUsers.ContainsKey(session.GetHabbo().Id))
        {
            if (MutedUsers[session.GetHabbo().Id] < UnixTimestamp.GetNow())
                MutedUsers.Remove(session.GetHabbo().Id);
            else
                return true;
        }
        if (session.GetHabbo().TimeMuted > 0 || RoomMuted && session.GetHabbo().Username != OwnerName)
            return true;
        return false;
    }

    /// <summary>
    /// pixelrp: one unit as a client that has never seen it needs it - the
    /// avatar, then everything worn on it. SendObjects sends each unit in the
    /// room this way; Invisibility sends one unit to a viewer it has just
    /// appeared to.
    /// </summary>
    /// <param name="employment">pixelrp corporations: the unit's job, read by
    /// the caller - SendObjects reads the whole room's in one query.</param>
    public void SendUnitTo(GameClient session, RoomUser unit, Plus.HabboHotel.Corporations.CorporationUtility.Employment? employment)
    {
        session.Send(new UsersComposer(unit));
        // pixelrp RP stats: the entering client learns every player's
        // health/energy for the HUD (bots/pets have no stats).
        if (!unit.IsBot && !unit.IsPet && unit.GetClient()?.GetHabbo() != null)
        {
            var habbo = unit.GetClient().GetHabbo();
            habbo.EnsureRpStatsLoaded();
            session.Send(new RpStatsComposer(unit.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax, (int)Math.Round(habbo.RpAggression), habbo.IsRpPassive ? 1 : 0, habbo.Rank >= 5 ? 1 : 0));
            // pixelrp corporations: the entering client learns everyone's
            // employment (infostand corp badge slot / profile row).
            if (employment != null)
                session.Send(Plus.HabboHotel.Corporations.CorporationUtility.ComposeFor(habbo.Id, employment));
        }
        if (unit.IsBot && unit.BotData.DanceId > 0)
            session.Send(new DanceComposer(unit, unit.BotData.DanceId));
        else if (!unit.IsBot && !unit.IsPet && unit.IsDancing)
            session.Send(new DanceComposer(unit, unit.DanceId));
        if (unit.IsAsleep)
            session.Send(new SleepComposer(unit, true));
        // pixelrp: a RESTING hand item too - an equipped weapon, an open
        // phone - not just a timed carry. Right after this unit's
        // UsersComposer is the only place the entering client is sure to
        // have the avatar to put it on; sent any earlier (AddAvatarToRoom
        // used to) it arrived before the avatar existed and was dropped, so
        // a player changing rooms lost their own stun gun from view and saw
        // nobody else's. RestingHandItemId is 0 for bots and pets, so a stale
        // CarryItemId with no timer still goes unsent.
        if (unit.CarryItemId > 0 && (unit.CarryTimer > 0 || unit.CarryItemId == unit.RestingHandItemId))
            session.Send(new CarryObjectComposer(unit.VirtualId, unit.CarryItemId));
        // pixelrp: not while dancing - a dance hides the enable for the room
        // (DanceEvent), so a newcomer saw both where everyone else saw one.
        if (!unit.IsBot && !unit.IsPet && unit.CurrentEffect > 0 && !unit.IsDancing)
            session.Send(new AvatarEffectComposer(unit.VirtualId, unit.CurrentEffect));
        else if (unit.IsBot && !unit.IsPet)
            // Bots always wear the identifier effect so entering players can spot them.
            session.Send(new AvatarEffectComposer(unit.VirtualId, RoomBot.IdentifierEffect));
    }

    /// <summary>
    /// pixelrp: the room's shape - what the client builds the room from, and the
    /// end of the blank screen. Sent first, ahead of the entering player's own
    /// lookups (GetRoomEntryDataEvent), not as the start of SendObjects.
    /// </summary>
    public void SendHeightmaps(GameClient session)
    {
        session.Send(new HeightMapComposer(GetGameMap().Model.Heightmap));
        session.Send(new FloorHeightMapComposer(GetGameMap().Model.GetRelativeHeightmap(), GetGameMap().StaticModel.WallHeight));
    }

    /// <param name="ownAvatarSent">pixelrp: the entering player was just added
    /// (AddAvatarToRoom), which already sent the whole room - them included -
    /// their avatar, stats, job, effect and hand item. Their own copies here
    /// would only arrive again, so they are left out.</param>
    public void SendObjects(GameClient session, bool ownAvatarSent = false)
    {
        var units = _roomUserManager.GetUserList().ToList();
        // pixelrp :invisible: a newcomer who isn't invisible is sent nobody
        // who is (Invisibility) - not their avatar, their step or their status.
        var hidden = session.GetHabbo().IsInvisible ? null : HiddenUnits(units);
        // the players sent, for their jobs just after (SendEmploymentsSoon)
        var employed = new List<int>();
        foreach (var user in units)
        {
            if (user == null || (ownAvatarSent && user.GetClient() == session))
                continue;
            if (hidden != null && hidden.Contains(user.VirtualId))
                continue;
            if (!user.IsBot && user.GetClient()?.GetHabbo() is { } habbo)
                employed.Add(habbo.Id);
            SendUnitTo(session, user, null);
        }
        SendEmploymentsSoon(session, employed);
        // pixelrp Movement V2: the step every walking unit is on right now,
        // BEFORE the status list - so the newcomer's client draws walkers on
        // their real timing from the first frame, instead of guessing with
        // stock Nitro for ~0.6s and then hopping them all forward.
        foreach (var catchUp in Movement.MovementV2Bridge.EntryCatchUp(this))
            SendUnlessHidden(session, catchUp, hidden);
        SendUnlessHidden(session, new UserUpdateComposer(_roomUserManager.GetUserList().ToList()), hidden);
        // pixelrp (Twist, 2026-10-28): a room entry's first message ends here -
        // the room's shape (RoomEntry), the newcomer and everybody already in
        // it, where they stand and how - so the client draws the floor and the
        // people on it in the same frame. The furni and the rest follow in the
        // next message and fill in on the floor as before. Nothing happens when
        // no batch is open.
        session.FlushBatch();
        session.Send(new ObjectsComposer(GetRoomItemHandler().GetFloor.ToArray(), this));
        // Opacity rides after the objects themselves, so the client has
        // something to apply it to. Only faded items travel; a room nobody
        // has built in sends a count of zero.
        var fadedItems = GetRoomItemHandler().GetFloor.Where(item => item.Alpha < 100).ToList();
        if (fadedItems.Count > 0)
            session.Send(new RpFurniAlphaComposer(fadedItems));
        // Definitions whose database record differs from the gamedata on disk,
        // re-sent to whoever just walked in. The client reads a furni's NAME,
        // its walkability and its height marker out of that file, which an edit
        // never touches - so a live edit patches everyone connected and then the
        // next login quietly reads the old values back.
        //
        // Two reasons a definition qualifies, and the second was missing:
        //
        //   * the Function Tool has edited it (rp_furni_function_log)
        //   * it carries the height marker at all
        //
        // The marker was relying entirely on the log, so one set by a migration
        // or by hand in SQL had no row, never travelled, and the ring silently
        // stopped appearing after any restart - it looked like the save had not
        // persisted when the furniture table had it all along. The log is an
        // audit trail; `furniture` is the source of truth, and rendering should
        // read the second.
        //
        // Only definitions actually in this room travel, which is normally a
        // handful and often none.
        var edited = PlusEnvironment.Game?.ItemManager?.EditedDefinitions;
        foreach (var definition in GetRoomItemHandler().GetWallAndFloor
                     // the room's item collections can hold nulls - every
                     // other loop over them guards, so this one does too
                     .Where(item => item != null)
                     .Select(item => item.Definition)
                     .Where(definition => definition != null
                                          && (definition.HeightMarker
                                              || (edited != null && edited.Contains(definition.Id))))
                     .DistinctBy(definition => definition.Id))
            session.Send(new RpFurniFunctionComposer(definition));
        session.Send(new ItemsComposer(GetRoomItemHandler().GetWall.ToArray(), this));
        // pixelrp jukebox: sent unconditionally, even with no jukebox in the
        // room — the packet is tiny and the client hides the panel itself
        // via present=false (see RoomJukeboxManager.BuildState).
        GetJukeboxManager().SendState(session);
        // pixelrp: an on-duty worker who just entered a room they may no
        // longer work in is clocked out on the spot, not on the next tick.
        // pixelrp (Twist, 2026-10-27): just after the room, on the writer's
        // thread - the check reads the job from the database (1-3 queries),
        // and the room was held back for it. Only for somebody on duty.
        if (Plus.HabboHotel.Corporations.ShiftManager.IsOnDuty(session.GetHabbo().Id))
            RoomTickWriter.QueueOrdered(() => Plus.HabboHotel.Corporations.ShiftManager.OnEnterRoom(session));
    }

    /// <summary>
    /// pixelrp (Twist, 2026-10-27): the jobs of the players a newcomer was
    /// just sent, read in one query on the writer's thread and sent after the
    /// room - everyone's employment used to be read before the furni went, and
    /// the room waited on it. A job badge shows a moment after its player.
    /// </summary>
    private void SendEmploymentsSoon(GameClient session, List<int> userIds)
    {
        if (userIds.Count == 0)
            return;
        RoomTickWriter.QueueOrdered(() =>
        {
            if (session.GetHabbo()?.CurrentRoom != this)
                return;
            foreach (var employment in Plus.HabboHotel.Corporations.CorporationUtility.GetEmployments(userIds)
                         .GroupBy(employment => employment.UserId)
                         .Select(group => group.First()))
                session.Send(Plus.HabboHotel.Corporations.CorporationUtility.ComposeFor(employment.UserId, employment));
        });
    }

    public void AddTent(uint tentId)
    {
        if (_tents.ContainsKey(tentId))
            _tents.Remove(tentId);
        _tents.Add(tentId, new());
    }

    public void RemoveTent(uint tentId)
    {
        if (!_tents.ContainsKey(tentId))
            return;
        var users = _tents[tentId];
        foreach (var user in users.ToList())
        {
            if (user == null || user.GetClient() == null || user.GetClient().GetHabbo() == null)
                continue;
            user.GetClient().GetHabbo().TentId = 0;
        }
        if (_tents.ContainsKey(tentId))
            _tents.Remove(tentId);
    }

    public void AddUserToTent(uint tentId, RoomUser user)
    {
        if (user != null && user.GetClient() != null && user.GetClient().GetHabbo() != null)
        {
            if (!_tents.ContainsKey(tentId))
                _tents.Add(tentId, new());
            if (!_tents[tentId].Contains(user))
                _tents[tentId].Add(user);
            user.GetClient().GetHabbo().TentId = tentId;
        }
    }

    public void RemoveUserFromTent(uint tentId, RoomUser user)
    {
        if (user != null && user.GetClient() != null && user.GetClient().GetHabbo() != null)
        {
            if (!_tents.ContainsKey(tentId))
                _tents.Add(tentId, new());
            if (_tents[tentId].Contains(user))
                _tents[tentId].Remove(user);
            user.GetClient().GetHabbo().TentId = 0;
        }
    }

    /// <summary>
    /// pixelrp: take a unit that is leaving the room out of every tent's list.
    /// Walking off the tent (RemoveUserFromTent) was the only way out of one,
    /// so a player who left the room from inside a tent stayed listed - the
    /// list holding their RoomUser - until the room unloaded. Never throws:
    /// it runs in the middle of a leave (RoomUserManager.RemoveRoomUser).
    /// </summary>
    public void RemoveUserFromTents(RoomUser user)
    {
        if (user == null || _tents.Count == 0)
            return;
        try
        {
            foreach (var members in _tents.Values.ToList())
                members.Remove(user);
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    public void SendToTent(int id, uint tentId, IServerPacket packet)
    {
        if (!_tents.ContainsKey(tentId))
            return;
        var sender = _roomUserManager.GetRoomUserByHabbo(id);
        foreach (var user in _tents[tentId].ToList())
        {
            if (user == null || user.GetClient() == null || user.GetClient().GetHabbo() == null || user.GetClient().GetHabbo().IgnoresComponent.IsIgnored(id) ||
                user.GetClient().GetHabbo().TentId != tentId || !user.CanSee(sender))
                continue;
            user.GetClient().Send(packet);
        }
    }

    public void SendPacket(IServerPacket packet, bool withRightsOnly = false)
    {
        if (packet == null || _roomUserManager == null)
            return;
        try
        {
            var users = _roomUserManager.GetUserList().ToList();
            // pixelrp: composed once, not once per viewer (SharedFrame).
            var frame = new SharedFrame(packet);
            // pixelrp :invisible: whoever can't see an invisible unit gets the
            // packet without it, or nothing when it was only about them.
            var visibleFrame = frame;
            var hidden = HiddenUnits(users);
            if (hidden != null)
            {
                var visiblePacket = IUnitPacket.Filter(packet, hidden);
                visibleFrame = visiblePacket == null ? null : ReferenceEquals(visiblePacket, packet) ? frame : new SharedFrame(visiblePacket);
            }
            foreach (var user in users)
            {
                if (user?.GetClient() is not { } client || user.IsBot)
                    continue;
                if (withRightsOnly && !CheckRights(client))
                    continue;
                (user.IsInvisible ? frame : visibleFrame)?.SendTo(client);
            }
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    public void SendPacket(List<IServerPacket> packets)
    {
        foreach (var packet in packets)
            SendPacket(packet);
    }

    /// <summary>pixelrp :invisible: a packet about <paramref name="subject"/>
    /// that names them by user id, which the send filter (IUnitPacket) can't
    /// read - their badges, their job, their favourite group. To everyone,
    /// unless they're invisible: then only to those who can see them.</summary>
    public void SendPacketAbout(Plus.HabboHotel.Users.Habbo subject, IServerPacket packet)
    {
        if (subject is not { IsInvisible: true })
        {
            SendPacket(packet);
            return;
        }
        foreach (var user in _roomUserManager.GetUserList().ToList())
            if (user is { IsInvisible: true } && user.GetClient() is { } client)
                client.Send(packet);
    }

    /// <summary>pixelrp :invisible: the virtual ids of the invisible units
    /// among <paramref name="users"/>, or null when there are none - the
    /// usual case, which keeps every send exactly as it was.</summary>
    public static HashSet<int> HiddenUnits(IEnumerable<RoomUser> users)
    {
        HashSet<int> hidden = null;
        foreach (var user in users)
            if (user is { IsInvisible: true })
                (hidden ??= new()).Add(user.VirtualId);
        return hidden;
    }

    /// <summary>pixelrp :invisible: <paramref name="packet"/> to one viewer in
    /// the room, without anyone invisible they can't see.</summary>
    public void SendToViewer(GameClient session, IServerPacket packet)
    {
        SendUnlessHidden(session, packet, session.GetHabbo().IsInvisible ? null : HiddenUnits(_roomUserManager.GetUserList()));
    }

    /// <summary>pixelrp :invisible: <paramref name="packet"/> to one viewer
    /// who can't see <paramref name="hidden"/> (null: sees everyone).</summary>
    private static void SendUnlessHidden(GameClient session, IServerPacket packet, ISet<int> hidden)
    {
        var filtered = hidden == null ? packet : IUnitPacket.Filter(packet, hidden);
        if (filtered != null)
            session.Send(filtered);
    }

    /// <summary>
    /// pixelrp: send several packets to everyone in the room (same audience as
    /// <see cref="SendPacket(IServerPacket, bool)"/>: real users, not bots) as
    /// ONE message each, with every packet composed ONCE per client kind.
    ///
    /// For the movement frame, which used to compose its status list and every
    /// step packet again for each viewer and send each as its own message: at
    /// 100 walkers that was ~20,000 sends a second on the one sender thread, and
    /// ~200 messages a second into every browser. Now it is one compose per
    /// packet and one message per viewer per frame.
    ///
    /// SAME BYTES, SAME ORDER. The bytes are exactly what Send would have
    /// written, back to back; the client's decoder already splits a message by
    /// each packet's length prefix and handles them in order. Viewers are
    /// grouped by client type and Revision, because those decide the header
    /// bytes. Outgoing packets are not encrypted (Rc4Client is only ever set),
    /// so identical bytes are valid for every client in a group.
    /// </summary>
    public void SendPacketsBatched(IReadOnlyList<IServerPacket> packets)
    {
        if (packets == null || packets.Count == 0)
            return;
        try
        {
            var users = _roomUserManager.GetUserList().ToList();
            // pixelrp :invisible: viewers who can't see an invisible unit are a
            // group of their own, sent the packets without it (IUnitPacket).
            var hidden = HiddenUnits(users);
            var groups = new Dictionary<(Type, Plus.Communication.Revisions.Revision, bool), List<GameClient>>();
            foreach (var user in users)
            {
                if (user?.GetClient() is not { } client || user.IsBot || client.Revision == null)
                    continue;
                var key = (client.GetType(), client.Revision, hidden != null && !user.IsInvisible);
                if (!groups.TryGetValue(key, out var list))
                    groups[key] = list = new List<GameClient>();
                list.Add(client);
            }

            foreach (var (key, clients) in groups)
            {
                try
                {
                    // Composed by the group's first client: its type and Revision
                    // are the group's, so its bytes are everyone's.
                    var parts = new List<byte[]>(packets.Count);
                    var total = 0;
                    foreach (var unfiltered in packets)
                    {
                        if (unfiltered == null)
                            continue;
                        var packet = key.Item3 ? IUnitPacket.Filter(unfiltered, hidden) : unfiltered;
                        if (packet == null)
                            continue;
                        var framed = clients[0].Frame(packet);
                        if (framed == null)
                            continue;
                        parts.Add(framed);
                        total += framed.Length;
                    }
                    if (total == 0)
                        continue;

                    var message = new byte[total];
                    var offset = 0;
                    foreach (var part in parts)
                    {
                        Buffer.BlockCopy(part, 0, message, offset, part.Length);
                        offset += part.Length;
                    }

                    foreach (var client in clients)
                    {
                        try
                        {
                            client.SendFramed(message);
                        }
                        catch (Exception e)
                        {
                            // One viewer's socket must not cost the others the frame.
                            ExceptionLogger.LogException(e);
                        }
                    }
                }
                catch (Exception e)
                {
                    // One group's failure must not cost the others the frame.
                    ExceptionLogger.LogException(e);
                }
            }
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    public void Dispose()
    {
        // pixelrp: marked unloaded before anything else, so MovementRegistry.
        // Attach refuses this room from here on - its user manager stays usable
        // for a while below, and an entry or a bot deploy meanwhile would
        // otherwise attach movement again after the Detach that follows.
        // Nothing else needs it unset: RoomManager.TryLoadRoom, its only other
        // reader, has already dropped this room from its list (UnloadRoom).
        Unloaded = true;
        // pixelrp Movement V2 (A7): detach from the movement scheduler FIRST.
        // Below, _gamemap.Dispose() nulls _userMap, GameMap, EffectMap, Model
        // and more. The V2 scheduler is a SINGLE hotel-wide thread, so if it
        // still held this room it would dereference that null and freeze
        // movement in every room at once. Detach closes the room under its
        // movement lock, drops it from the scheduler heap and clears its queued
        // work, all before any resource here is torn down.
        // No-op for rooms V2 never attached to, so this is safe with the flag off.
        Movement.MovementRegistry.Detach(RoomId);
        // pixelrp turfs: a claim cannot outlive the room it is running in.
        Gangs.TurfManager.Forget(RoomId);

        SendPacket(new CloseConnectionComposer());
        if (!MDisposed)
        {
            IsCrashed = false;
            MDisposed = true;
            /* TODO: Needs reviewing */
            try
            {
                if (ProcessTask != null && ProcessTask.IsCompleted)
                    ProcessTask.Dispose();
            }
            catch { }
            TonerData = null;
            MoodlightData = null;
            if (MutedUsers.Count > 0)
                MutedUsers.Clear();
            if (_tents.Count > 0)
                _tents.Clear();
            if (UsersWithRights.Count > 0)
                UsersWithRights.Clear();
            if (_gameManager != null)
            {
                _gameManager.Dispose();
                _gameManager = null;
            }
            if (_freeze != null)
            {
                _freeze.Dispose();
                _freeze = null;
            }
            if (_soccer != null)
            {
                _soccer.Dispose();
                _soccer = null;
            }
            if (_banzai != null)
            {
                _banzai.Dispose();
                _banzai = null;
            }
            if (_gamemap != null)
            {
                _gamemap.Dispose();
                _gamemap = null;
            }
            if (_gameItemHandler != null)
            {
                _gameItemHandler.Dispose();
                _gameItemHandler = null;
            }

            // Room Data?
            if (Teambanzai != null)
            {
                Teambanzai.Dispose();
                Teambanzai = null;
            }
            if (Teamfreeze != null)
            {
                Teamfreeze.Dispose();
                Teamfreeze = null;
            }
            if (_roomUserManager != null)
            {
                _roomUserManager.Dispose();
                _roomUserManager = null;
            }
            if (_roomItemHandling != null)
            {
                _roomItemHandling.Dispose();
                _roomItemHandling = null;
            }
            if (WordFilterList.Count > 0)
                WordFilterList.Clear();
            if (_filterComponent != null)
                _filterComponent.Cleanup();
            if (_wiredComponent != null)
                _wiredComponent.Cleanup();
            if (_bansComponent != null)
                _bansComponent.Cleanup();
            if (_tradingComponent != null)
                _tradingComponent.Cleanup();
        }
    }
}