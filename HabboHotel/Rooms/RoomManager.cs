using System.Collections.Concurrent;
using System.Data;
using Microsoft.Extensions.Logging;
using Plus.Core;
using Plus.Core.Language;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms;

public class RoomManager : IRoomManager
{
    private readonly ILogger<RoomManager> _logger;
    private readonly IDatabase _database;
    private readonly ILanguageManager _languageManager;

    /// <summary>
    /// pixelrp: one gate per room for loading it (TryLoadRoom) and for the idle
    /// unload (UnloadIdleRoom). It was one lock for the whole hotel, so a room
    /// being loaded held up every other room's load behind it - the database
    /// read of every item, bot and pet - and two players going into two cold
    /// rooms waited on each other. Now only loads of the same room queue, and
    /// the second finds the first's room.
    /// </summary>
    private readonly ConcurrentDictionary<uint, object> _roomGates = new();

    // Loads run side by side now, and the custom models are added by them.
    private readonly ConcurrentDictionary<string, RoomModel> _roomModels;

    /// <summary>
    /// pixelrp: rooms to load ahead of somebody arriving (WarmInBackground), on
    /// one worker of their own - a load is a database read, and neither the
    /// packet threads nor the thread pool should wait on one nobody is waiting
    /// for yet. Bounded: a warm-up that does not fit is skipped, and the room
    /// loads on entry as it always did.
    /// </summary>
    private readonly BlockingCollection<Func<IEnumerable<uint>>> _warmQueue = new(256);

    private readonly ConcurrentDictionary<uint, Room> _rooms;

    // pixelrp: there is no "previous cycle" before the first one, so the first
    // OnCycle must not be measured against anything. Left at DateTime.MinValue
    // it reported ~2000 YEARS and — because an out-of-range double->int
    // conversion yields int.MinValue on x64 — printed -2147483648ms; seeded at
    // construction it still reported the ~1.1s of boot between DI and the first
    // tick. Either way it was one bogus stall warning per boot, sitting in the
    // log next to the real ones. Skip the comparison entirely instead.
    private DateTime _cycleLastExecution = DateTime.Now;
    private bool _cycleFirstRun = true;


    public RoomManager(ILogger<RoomManager> logger, IDatabase database, ILanguageManager languageManager)
    {
        _logger = logger;
        _database = database;
        _languageManager = languageManager;
        _roomModels = new();
        _rooms = new();
        new Thread(WarmRooms) { IsBackground = true, Name = "RoomWarm" }.Start();
    }

    public int Count => _rooms.Count;

    public void OnCycle()
    {
        try
        {
            var sinceLastTime = DateTime.Now - _cycleLastExecution;
            if (sinceLastTime.TotalMilliseconds >= 500)
            {
                // TEMP stall telemetry (2026-08-25): the loop polls every 5ms,
                // so firing much later than 500ms means the game-cycle thread
                // itself stalled (GC pause or ClientManager.OnCycle blocking),
                // freezing movement in every room at once.
                // Clamped so a clock jump (NTP step, suspend/resume) reports a
                // useless-but-sane number instead of overflowing the cast.
                if (sinceLastTime.TotalMilliseconds > 750 && !_cycleFirstRun)
                    _logger.LogWarning("[stall] Room cycle fired {Ms}ms after the previous one (target 500ms)",
                        (int)Math.Clamp(sinceLastTime.TotalMilliseconds, 0, int.MaxValue));
                _cycleLastExecution = DateTime.Now;
                _cycleFirstRun = false;
                foreach (var room in _rooms.Values.ToList())
                {
                    if (room.IsCrashed)
                        continue;
                    if (room.ProcessTask == null || room.ProcessTask.IsCompleted)
                    {
                        room.ProcessTask?.Dispose();
                        room.ProcessTask = new(room.ProcessRoom);
                        room.ProcessTaskStarted = DateTime.Now;
                        room.ProcessTask.Start();
                        room.IsLagging = 0;
                    }
                    else
                    {
                        room.IsLagging++;
                        // TEMP stall telemetry: a skipped tick sends no movement
                        // updates — this is the freeze players see.
                        _logger.LogWarning("[stall] Room {Id} tick skipped - ProcessRoom still running after {Ms}ms (lagging={Lagging})",
                            room.Id, (int)(DateTime.Now - room.ProcessTaskStarted).TotalMilliseconds, room.IsLagging);
                        if (room.IsLagging >= 30)
                        {
                            room.IsCrashed = true;
                            UnloadRoom(room.Id);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    public void LoadModels()
    {
        if (_roomModels.Count > 0)
            _roomModels.Clear();
        using var dbClient = _database.GetQueryReactor();
        dbClient.SetQuery("SELECT id,door_x,door_y,door_z,door_dir,heightmap,club_only,poolmap,`wall_height` FROM `room_models` WHERE `custom` = '0'");
        var data = dbClient.GetTable();
        if (data == null)
            return;
        foreach (DataRow row in data.Rows)
        {
            var model = Convert.ToString(row["id"]);
            _roomModels[model] = new(model, Convert.ToInt32(row["door_x"]), Convert.ToInt32(row["door_y"]), (double)row["door_z"], Convert.ToInt32(row["door_dir"]),
                Convert.ToString(row["heightmap"]), ConvertExtensions.EnumToBool(row["club_only"].ToString()), Convert.ToInt32(row["wall_height"]), false);
        }
    }

    public bool LoadModel(string id)
    {
        DataRow row = null;
        using var dbClient = _database.GetQueryReactor();
        dbClient.SetQuery("SELECT id,door_x,door_y,door_z,door_dir,heightmap,club_only,poolmap,`wall_height` FROM `room_models` WHERE `custom` = '1' AND `id` = @modelId LIMIT 1");
        dbClient.AddParameter("modelId", id);
        row = dbClient.GetRow();
        if (row == null)
            return false;
        var model = Convert.ToString(row["id"]);
        _roomModels.TryAdd(model, new(model, Convert.ToInt32(row["door_x"]), Convert.ToInt32(row["door_y"]), Convert.ToDouble(row["door_z"]), Convert.ToInt32(row["door_dir"]),
            Convert.ToString(row["heightmap"]), ConvertExtensions.EnumToBool(row["club_only"].ToString()), Convert.ToInt32(row["wall_height"]), true));
        return true;
    }

    public void ReloadModel(string id)
    {
        if (!_roomModels.ContainsKey(id))
        {
            LoadModel(id);
            return;
        }
        _roomModels.TryRemove(id, out _);
        LoadModel(id);
    }

    public bool TryGetModel(string id, out RoomModel model)
    {
        if (_roomModels.TryGetValue(id, out model))
            return true;

        // Try to load this model.
        if (LoadModel(id))
        {
            if (TryGetModel(id, out var customModel))
            {
                model = customModel;
                return true;
            }
        }
        model = null;
        return false;
    }

    public void UnloadRoom(uint roomId)
    {
        if (_rooms.TryRemove(roomId, out var room)) room.Dispose();
    }

    /// <summary>
    /// pixelrp: unload a room and bring everyone in it straight back, freshly
    /// loaded - a floor plan save, :unload, a group deleted.
    ///
    /// UnloadRoom alone leaves the players in it pointing at the room it has
    /// just disposed (Habbo.CurrentRoom), whose user list is gone: the next
    /// thing their client sends about the room - the typing indicator going
    /// off, a whisper to them - throws, and a throw there drops the connection
    /// and puts up the disconnected screen. And players are never out of a
    /// room. So each is taken out of it and forwarded back in.
    /// </summary>
    public void ReloadRoom(Room room)
    {
        var usersToReturn = room.GetRoomUserManager().GetRoomUsers().ToList();
        UnloadRoom(room.Id);
        foreach (var user in usersToReturn)
        {
            var client = user?.GetClient();
            if (client?.GetHabbo() == null)
                continue;
            client.GetHabbo().CurrentRoom = null;
            client.SendRoomForward(room.Id);
        }
    }

    public bool TryLoadRoom(uint roomId, out Room room)
    {
        var gate = GateFor(roomId);
        lock (gate)
        {
            if (_rooms.TryGetValue(roomId, out var inst))
            {
                if (!inst.Unloaded)
                {
                    inst.MarkHandedOut();
                    room = inst;
                    return true;
                }
                room = null;
                return false;
            }
            if (!RoomFactory.TryGetData(roomId, out var data))
            {
                // No such room: its gate goes, or any id a client cares to ask
                // about (room settings take one straight off the wire) stays
                // in the map for good. Only ever for a room that is not there,
                // so no two loads of a real room can end up on two gates.
                _roomGates.TryRemove(new KeyValuePair<uint, object>(roomId, gate));
                room = null;
                return false;
            }
            var myInstance = new Room(data);
            if (_rooms.TryAdd(roomId, myInstance))
            {
                myInstance.MarkHandedOut();
                room = myInstance;
                return true;
            }
            room = null;
            return false;
        }
    }

    private object GateFor(uint roomId) => _roomGates.GetOrAdd(roomId, _ => new object());

    /// <summary>
    /// How long after a room was last handed out (TryLoadRoom) the idle unload
    /// leaves it be: a player handed a room is on their way in.
    /// </summary>
    private const long EntryGraceMs = 30_000;

    /// <summary>
    /// pixelrp: the unload of a room left empty (Room.ProcessRoom). It came
    /// from the room's own tick with no hold on anyone entering it, so a player
    /// handed the room a moment before (TryLoadRoom) could arrive in it after
    /// it was disposed - its map gone, the entry throwing. Under the room's
    /// gate it either goes before anyone is handed it, and they load it fresh,
    /// or after, and it sees them coming and stays. False when it stayed.
    /// </summary>
    public bool UnloadIdleRoom(Room room)
    {
        lock (GateFor(room.Id))
        {
            if (room.HandedOutWithin(EntryGraceMs) || room.UserCount > 0)
                return false;
            if (_rooms.TryRemove(new KeyValuePair<uint, Room>(room.Id, room)))
                room.Dispose();
            return true;
        }
    }

    /// <summary>
    /// pixelrp: load a room somebody is about to enter while they are still
    /// on their way (a forward, a booth), so their entry finds it loaded. It
    /// otherwise loaded on the entry itself, after they had already left the
    /// room they were in.
    /// </summary>
    public void WarmInBackground(uint roomId)
    {
        if (roomId != 0 && !_rooms.ContainsKey(roomId))
            _warmQueue.TryAdd(() => new[] { roomId });
    }

    /// <summary>
    /// As <see cref="WarmInBackground(uint)"/>, for rooms that take a database
    /// read to find - looked for on the worker, not the caller's thread.
    /// </summary>
    public void WarmInBackground(Func<IEnumerable<uint>> findRooms) => _warmQueue.TryAdd(findRooms);

    private void WarmRooms()
    {
        foreach (var findRooms in _warmQueue.GetConsumingEnumerable())
        {
            try
            {
                foreach (var roomId in findRooms())
                {
                    if (roomId != 0 && !_rooms.ContainsKey(roomId))
                        TryLoadRoom(roomId, out _);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Loading a room ahead of an entry failed.");
            }
        }
    }


    public List<Room> SearchGroupRooms(string query)
    {
        return _rooms.Values.Where(x => x.Group != null && x.Group.Name.ToLower().Contains(query.ToLower()) && x.Access != RoomAccess.Invisible).OrderByDescending(x => x.UsersNow).Take(50).ToList();
    }

    public List<Room> SearchTaggedRooms(string query)
    {
        return _rooms.Values.Where(x => x.Tags.Contains(query) && x.Access != RoomAccess.Invisible).OrderByDescending(x => x.UsersNow).Take(50).ToList();
    }

    public List<Room> GetPopularRooms(int category, int amount = 50)
    {
        return _rooms.Values.Where(x => x.UsersNow > 0 && x.Access != RoomAccess.Invisible).OrderByDescending(x => x.UsersNow).Take(amount).ToList();
    }

    public List<Room> GetPopularRatedRooms(int amount = 50)
    {
        return _rooms.Values.Where(x => x.Access != RoomAccess.Invisible).OrderByDescending(x => x.Score).OrderByDescending(x => x.UsersNow).Take(amount).ToList();
    }

    public List<Room> GetGroupRooms(int amount = 50)
    {
        return _rooms.Values.Where(x => x.Group != null && x.Access != RoomAccess.Invisible).OrderByDescending(x => x.Score).Take(amount).ToList();
    }

    public List<Room> GetRoomsByIds(List<uint> ids, int amount = 50)
    {
        return _rooms.Values.Where(x => ids.Contains(x.Id) && x.Access != RoomAccess.Invisible).OrderByDescending(x => x.UsersNow).Take(amount).ToList();
    }

    public Room TryGetRandomLoadedRoom()
    {
        return _rooms.Values.Where(x => x.UsersNow > 0 && x.Access != RoomAccess.Invisible && x.UsersNow < x.UsersMax).OrderByDescending(x => x.UsersNow).FirstOrDefault();
    }


    public bool TryGetRoom(uint roomId, out Room room) => _rooms.TryGetValue(roomId, out room);

    public RoomData CreateRoom(GameClient session, string name, string description, int category, int maxVisitors, int tradeSettings, RoomModel model, string wallpaper = "0.0", string floor = "0.0",
        string landscape = "0.0", int wallthick = 0, int floorthick = 0)
    {
        if (name.Length < 3)
        {
            session.SendNotification(_languageManager.TryGetValue("room.creation.name.too_short"));
            return null;
        }
        var roomId = 0u;
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery(
                "INSERT INTO `rooms` (`roomtype`,`caption`,`description`,`owner`,`model_name`,`category`,`users_max`,`trade_settings`) VALUES ('private',@caption,@description,@UserId,@model,@category,@usersmax,@tradesettings)");
            dbClient.AddParameter("caption", name);
            dbClient.AddParameter("description", description);
            dbClient.AddParameter("UserId", session.GetHabbo().Id);
            dbClient.AddParameter("model", model.Id);
            dbClient.AddParameter("category", category);
            dbClient.AddParameter("usersmax", maxVisitors);
            dbClient.AddParameter("tradesettings", tradeSettings);
            roomId = Convert.ToUInt32(dbClient.InsertQuery());
        }
        // hidewall: true matches the DB default (SQL update 20) so a freshly
        // created room hides its walls before its first reload from the DB.
        // allow_medical/police/staff default to '1' in the DB, so mirror that
        // here too (corporation_id's default of 0 is already the int default).
        var data = new RoomData(roomId, name, model.Id, session.GetHabbo().Username, session.GetHabbo().Id, "", 0, "public", "open", 0, maxVisitors, category, description, string.Empty,
            floor, landscape, true, true, false, true, wallthick, floorthick, wallpaper, 1, 1, 1, 1, 1, 1, 1, 8, tradeSettings, true, true, true, true, true, true, true, 0, 0, true, model)
        {
            AllowMedical = true,
            AllowPolice = true,
            AllowStaff = true
        };
        return data;
    }

    public ICollection<Room> GetRooms() => _rooms.Values;

    public void Dispose()
    {
        var length = _rooms.Count;
        var i = 0;
        foreach (var room in _rooms.Values.ToList())
        {
            if (room == null)
                continue;
            UnloadRoom(room.Id);
            Console.Clear();
            _logger.LogInformation("<<- SERVER SHUTDOWN ->> ROOM ITEM SAVE: " + string.Format("{0:0.##}", (double)i / length * 100) + "%");
            i++;
        }
        _logger.LogInformation("Done disposing rooms!");
    }
}