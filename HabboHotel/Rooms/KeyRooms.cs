using System.Data;
using NLog;
using Plus.Core;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: the rooms that are always loaded - every corporation's rooms
/// (their headquarters, the hospital among them) and the jail.
///
/// WHY. A room nobody is in is unloaded after a while (Room.IdleUnloadTicks),
/// and the next person in waits while it is read back from the database: every
/// item, plus a query per bot and pet. These are the rooms people are SENT to,
/// often when nobody else is there - a knocked-out player to the hospital, an
/// arrested one to jail, a worker to their HQ. Loaded at startup and never
/// unloaded for being empty, a trip there never waits on a load.
///
/// THE PRICE. They stay in memory - quiet while empty (Room.QuietAfterTicks),
/// so it costs memory, not ticking. And an edit made to one straight in the
/// database, not through the game, is not seen until the room is reloaded:
/// :unload in it, or a restart.
/// </summary>
public static class KeyRooms
{
    private static readonly ILogger Log = LogManager.GetLogger("Plus.HabboHotel.Rooms.KeyRooms");

    /// <summary>Never unloaded for being empty.</summary>
    public static bool StaysLoaded(RoomData room) => room.CorporationId > 0 || room.IsJailRoom;

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
                dbClient.SetQuery("SELECT `id` FROM `rooms` WHERE `corporation_id` > 0 OR `rp_jail_room` = '1'");
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
    }
}
