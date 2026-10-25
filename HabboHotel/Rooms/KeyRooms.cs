using System.Data;
using NLog;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Core;
using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: the rooms that are always loaded - every corporation's rooms
/// (their headquarters, the hospital among them), the jail, and every PixelRP
/// room (the city: owned by the PixelRP account) with an arrow in it.
///
/// WHY. A room nobody is in is unloaded after a while (Room.IdleUnloadTicks),
/// and the next person in waits while it is read back from the database: every
/// item, bot and pet. These are the rooms people are SENT to, often when
/// nobody else is there - a knocked-out player to the hospital, an arrested
/// one to jail, a worker to their HQ, anyone stepping onto an arrow (the city's
/// streets join up by arrows, so their rooms all have one). Loaded at startup
/// and never unloaded for being empty, a trip there never waits on a load.
///
/// THE PRICE. They stay in memory - quiet while empty (Room.QuietAfterTicks),
/// so it costs memory, not ticking. And an edit made to one straight in the
/// database, not through the game, is not seen until the room is reloaded:
/// :unload in it, or a restart.
///
/// Arrows count only in the city (Twist, 2026-10-25): arrows are on sale from
/// Builder up, so any room anyone put one in stayed loaded for good, and the
/// city is growing. A player's or staff member's own room with an arrow loads
/// when someone goes in, like any room; "Set as public room" makes one part of
/// the city.
/// </summary>
public static class KeyRooms
{
    private static readonly ILogger Log = LogManager.GetLogger("Plus.HabboHotel.Rooms.KeyRooms");

    /// <summary>
    /// Never unloaded for being empty. The arrow test reads the room's own
    /// furni, so an arrow placed after startup counts too.
    /// </summary>
    public static bool StaysLoaded(Room room) =>
        room.CorporationId > 0 || room.IsJailRoom
        || (PixelRpRooms.IsPixelRpRoom(room)
            && room.GetRoomItemHandler().GetFloor.Any(item => item.Definition?.InteractionType == InteractionType.Arrow));

    /// <summary>
    /// Every furni type in the loaded key rooms, for a player logging in to
    /// download ahead (RpCityFurniComposer): these are the rooms they will be
    /// sent to and walk through. Read from the rooms' furni, so it is what is
    /// there now.
    /// </summary>
    public static RpCityFurniComposer CityFurni()
    {
        var floor = new HashSet<int>();
        var wall = new HashSet<int>();
        foreach (var room in PlusEnvironment.Game.RoomManager.GetRooms())
        {
            if (room.MDisposed || room.Unloaded || !StaysLoaded(room))
                continue;
            var items = room.GetRoomItemHandler();
            foreach (var item in items.GetFloor)
                if (item.Definition != null)
                    floor.Add(item.Definition.SpriteId);
            foreach (var item in items.GetWall)
                if (item.Definition != null)
                    wall.Add(item.Definition.SpriteId);
        }
        return new RpCityFurniComposer(floor, wall);
    }

    /// <summary>
    /// Load every key room, one after another, on a thread of its own: startup
    /// does not wait for it, and a player entering one meanwhile simply finds
    /// it loading (or loads it).
    /// </summary>
    public static void PreloadInBackground() =>
        new Thread(Preload) { IsBackground = true, Name = "PixelRPKeyRooms" }.Start();

    private static void Preload()
    {
        try
        {
            DataTable table;
            using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
            {
                // rp_jail_room is ENUM('0','1'): compared to a bare 1 MySQL would
                // read it by position and match the '0' rows - every non-jail room.
                // The same rooms StaysLoaded keeps: arrows only in the city.
                dbClient.SetQuery("SELECT `id` FROM `rooms` WHERE `corporation_id` > 0 OR `rp_jail_room` = '1'"
                    + " OR (`owner` = @pixelrp AND `id` IN (SELECT `items`.`room_id` FROM `items`"
                    + " JOIN `furniture` ON `furniture`.`id` = `items`.`base_item`"
                    + " WHERE `furniture`.`interaction_type` = 'arrow'))");
                dbClient.AddParameter("pixelrp", PixelRpRooms.AccountId);
                table = dbClient.GetTable();
            }
            if (table == null)
                return;

            var loaded = 0;
            foreach (DataRow row in table.Rows)
            {
                try
                {
                    if (PlusEnvironment.Game.RoomManager.TryLoadRoom(Convert.ToUInt32(row["id"]), out _))
                        loaded++;
                }
                catch (Exception e)
                {
                    // One room that will not load must not keep the rest out.
                    ExceptionLogger.LogException(e);
                }
            }
            Log.Info($"Key rooms loaded: {loaded} of {table.Rows.Count}.");
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
        try
        {
            ItemTeleporterFinder.PreloadArrowLinks();
        }
        catch (Exception e)
        {
            // Only a head start: each arrow still looks its link up on first use.
            ExceptionLogger.LogException(e);
        }
    }
}
