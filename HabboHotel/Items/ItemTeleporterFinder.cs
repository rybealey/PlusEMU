using System.Collections.Concurrent;
using System.Data;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items;

public static class ItemTeleporterFinder
{
    /// <summary>
    /// pixelrp: every link looked up so far, teleporter id to twin id. A link
    /// is written once, when the pair is bought (ItemFactory), and nothing
    /// changes or deletes it, so the first answer is the answer for good - and
    /// an arrow step no longer waits on the database, on the thread that sends
    /// every room's walking. Only found links are kept: a lookup that finds
    /// none asks again next time. A link edited by hand in the database is seen
    /// after a restart.
    /// </summary>
    private static readonly ConcurrentDictionary<uint, uint> Links = new();

    public static uint GetLinkedTele(uint teleId)
    {
        if (Links.TryGetValue(teleId, out var cached))
            return cached;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery($"SELECT `tele_two_id` FROM `room_items_tele_links` WHERE `tele_one_id` = '{teleId}' LIMIT 1");
        var row = dbClient.GetRow();
        if (row == null) return 0;
        var linkId = Convert.ToUInt32(row[0]);
        Links[teleId] = linkId;
        return linkId;
    }

    /// <summary>
    /// pixelrp: every arrow's link, in one query, at startup (KeyRooms). The
    /// first step onto each arrow after a restart used to look its link up
    /// right there, on the thread that sends every room's walking.
    /// </summary>
    public static void PreloadArrowLinks()
    {
        DataTable table;
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT `room_items_tele_links`.`tele_one_id`, `room_items_tele_links`.`tele_two_id` FROM `room_items_tele_links`"
                + " JOIN `items` ON `items`.`id` = `room_items_tele_links`.`tele_one_id`"
                + " JOIN `furniture` ON `furniture`.`id` = `items`.`base_item`"
                + " WHERE `furniture`.`interaction_type` = 'arrow'");
            table = dbClient.GetTable();
        }
        if (table == null)
            return;
        foreach (DataRow row in table.Rows)
            Links[Convert.ToUInt32(row[0])] = Convert.ToUInt32(row[1]);
    }

    public static uint GetTeleRoomId(uint teleId, Room pRoom)
    {
        if (pRoom.GetRoomItemHandler().GetItem(teleId) != null)
            return pRoom.RoomId;
        // pixelrp: the loaded rooms' furni before the database. Rooms with an
        // arrow never unload (KeyRooms), so an arrow's twin is nearly always
        // here, and a room's own furni is the fresher record of the two.
        foreach (var room in PlusEnvironment.Game.RoomManager.GetRooms())
        {
            if (room.MDisposed || room.Unloaded)
                continue;
            if (room.GetRoomItemHandler().GetItem(teleId) != null)
                return room.RoomId;
        }
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery($"SELECT `room_id` FROM `items` WHERE `id` = {teleId} LIMIT 1");
        var row = dbClient.GetRow();
        if (row == null) return 0;
        return Convert.ToUInt32(row[0]);
    }

    public static bool IsTeleLinked(uint teleId, Room pRoom)
    {
        var linkId = GetLinkedTele(teleId);
        if (linkId == 0) return false;
        var item = pRoom.GetRoomItemHandler().GetItem(linkId);
        if (item != null && item.Definition.InteractionType == InteractionType.Teleport)
            return true;
        var roomId = GetTeleRoomId(linkId, pRoom);
        if (roomId == 0) return false;
        return true;
    }
}