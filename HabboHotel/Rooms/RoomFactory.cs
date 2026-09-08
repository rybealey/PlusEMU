using System.Data;

namespace Plus.HabboHotel.Rooms;

public static class RoomFactory
{
    // Rooms imported from the previous stack can carry MySQL's invalid-enum
    // marker (an empty string) or NULL in numeric columns; Convert.ToInt32
    // throws on both, which killed every packet handler that loads a user's
    // rooms (room creation, My World search). Fall back instead of throwing.
    private static int ToInt(object value, int fallback = 0) =>
        int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;

    // Columns migrated from enum('0','1') to BOOLEAN come back from
    // MySqlConnector as .NET bool (tinyint(1) with TreatTinyAsBoolean, the
    // default), so Convert.ToString yields "True"/"False" and a == "1"
    // comparison silently reads every row as false — hide-walls "reappearing
    // after room load" was this. Handle both column shapes.
    private static bool ToBool(object value) =>
        value is bool b ? b : Convert.ToString(value) == "1";


    public static List<RoomData> GetRoomsDataByOwnerSortByName(int ownerId)
    {
        var data = new List<RoomData>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `username`, `rooms`.* FROM `users` INNER JOIN `rooms` ON `owner` = `users`.`id` WHERE `users`.`id` = @ownerid ORDER BY `caption`;");
        dbClient.AddParameter("ownerid", ownerId);
        var rooms = dbClient.GetTable();
        if (rooms != null)
        {
            foreach (DataRow row in rooms.Rows)
            {
                var mapped = FromRow(row);
                if (mapped != null) data.Add(mapped);
            }
        }
        return data;
    }


    /// <summary>
    /// One `rooms` row as RoomData, preferring the live instance when the room
    /// is loaded so the user count and anything else the room owns is current.
    /// Returns null when the model is unknown - such a room cannot be entered
    /// and has no business in a listing.
    /// </summary>
    private static RoomData? FromRow(DataRow row)
    {
        if (PlusEnvironment.Game.RoomManager.TryGetRoom(Convert.ToUInt32(row["id"]), out var room))
            return room;
        if (!PlusEnvironment.Game.RoomManager.TryGetModel(Convert.ToString(row["model_name"]), out var model)) return null;
        return new(Convert.ToUInt32(row["id"]), Convert.ToString(row["caption"]), Convert.ToString(row["model_name"]), Convert.ToString(row["username"]),
            ToInt(row["owner"]),
            Convert.ToString(row["password"]), ToInt(row["score"]), Convert.ToString(row["roomtype"]), Convert.ToString(row["state"]), ToInt(row["users_now"]),
            ToInt(row["users_max"]), ToInt(row["category"]), Convert.ToString(row["description"]), Convert.ToString(row["tags"]), Convert.ToString(row["floor"]),
            Convert.ToString(row["landscape"]), ToBool(row["allow_pets"]), ToBool(row["allow_pets_eat"]), ToBool(row["room_blocking_disabled"]),
            ToBool(row["allow_hidewall"]),
            ToInt(row["wallthick"]), ToInt(row["floorthick"]), Convert.ToString(row["wallpaper"]), ToInt(row["mute_settings"], 1),
            ToInt(row["ban_settings"], 1),
            ToInt(row["kick_settings"], 1), ToInt(row["chat_mode"]), ToInt(row["chat_size"]), ToInt(row["chat_speed"]),
            ToInt(row["chat_extra_flood"]),
            ToInt(row["chat_hearing_distance"], 100), ToInt(row["trade_settings"]), ToBool(row["push_enabled"]),
            ToBool(row["pull_enabled"]),
            ToBool(row["spush_enabled"]), ToBool(row["spull_enabled"]), ToBool(row["enables_enabled"]),
            ToBool(row["respect_notifications_enabled"]),
            ToBool(row["pet_morphs_allowed"]), ToInt(row["group_id"]), ToInt(row["sale_price"]), ToBool(row["lay_enabled"]), model)
        {
            IsSafeZone = ToBool(row["is_safe_zone"]),
            CorporationId = Convert.ToInt32(row["corporation_id"]),
            AllowMedical = ToBool(row["allow_medical"]),
            AllowPolice = ToBool(row["allow_police"]),
            AllowStaff = ToBool(row["allow_staff"])
        };
    }

    /// <summary>
    /// Every room in a navigator category, from the DATABASE rather than from
    /// the loaded-room dictionary.
    ///
    /// RoomManager only holds rooms that are loaded, and a room unloads after
    /// 60 idle cycles with nobody in it (Room.ProcessRoom) - so listing from
    /// there means an empty room drops out of the navigator entirely a minute
    /// after the last person leaves, which is exactly the "my room is not in
    /// the navigator" complaint. Search never had the problem because it has
    /// always queried the table.
    ///
    /// Occupied rooms first, then by score, so a busy room still leads the
    /// category. Loaded rooms come back as their live instance, so counts are
    /// current rather than whatever was last persisted.
    /// </summary>
    public static List<RoomData> GetRoomsDataByCategory(int categoryId, int limit)
    {
        var data = new List<RoomData>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `username`, `rooms`.* FROM `rooms` INNER JOIN `users` ON `rooms`.`owner` = `users`.`id` " +
                          "WHERE `rooms`.`category` = @category AND `rooms`.`state` != 'invisible' " +
                          "ORDER BY `rooms`.`users_now` DESC, `rooms`.`score` DESC LIMIT @limit;");
        dbClient.AddParameter("category", categoryId);
        dbClient.AddParameter("limit", limit);
        var rooms = dbClient.GetTable();
        if (rooms == null) return data;
        foreach (DataRow row in rooms.Rows)
        {
            var mapped = FromRow(row);
            if (mapped != null) data.Add(mapped);
        }
        return data;
    }

    /// <summary>
    /// The rooms the navigator recommends: best rated first, from the DATABASE
    /// for the same reason as the category listing - a recommendation that
    /// disappears the moment its room empties is not much of one.
    ///
    /// Occupancy is the tie-break rather than the sort, so a well-rated empty
    /// room still ranks above a poorly-rated busy one. "Popular" is the view
    /// that ranks purely by who is in a room right now, and it still lists only
    /// occupied rooms - deliberately.
    /// </summary>
    public static List<RoomData> GetRoomsDataRecommended(int limit)
    {
        var data = new List<RoomData>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `username`, `rooms`.* FROM `rooms` INNER JOIN `users` ON `rooms`.`owner` = `users`.`id` " +
                          "WHERE `rooms`.`state` != 'invisible' " +
                          "ORDER BY `rooms`.`score` DESC, `rooms`.`users_now` DESC LIMIT @limit;");
        dbClient.AddParameter("limit", limit);
        var rooms = dbClient.GetTable();
        if (rooms == null) return data;
        foreach (DataRow row in rooms.Rows)
        {
            var mapped = FromRow(row);
            if (mapped != null) data.Add(mapped);
        }
        return data;
    }

    public static bool TryGetData(uint roomId, out RoomData data)
    {
        if (PlusEnvironment.Game.RoomManager.TryGetRoom(roomId, out var room))
        {
            data = room;
            return true;
        }
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT `rooms`.*, `users`.`username` FROM `rooms` INNER JOIN `users` ON `users`.`id` = `rooms`.`owner` WHERE `rooms`.`id` = @id LIMIT 1");
            dbClient.AddParameter("id", roomId);
            var row = dbClient.GetRow();
            if (row != null)
            {
                RoomModel model = null;
                if (!PlusEnvironment.Game.RoomManager.TryGetModel(Convert.ToString(row["model_name"]), out model))
                {
                    data = null;
                    return false;
                }
                
                var username = !string.IsNullOrEmpty(Convert.ToString(row["username"])) ? Convert.ToString(row["username"]) : "Habboon";
                data = new(Convert.ToUInt32(row["id"]), Convert.ToString(row["caption"]), Convert.ToString(row["model_name"]), username, ToInt(row["owner"]),
                    Convert.ToString(row["password"]), ToInt(row["score"]), Convert.ToString(row["roomtype"]), Convert.ToString(row["state"]), ToInt(row["users_now"]),
                    ToInt(row["users_max"]), ToInt(row["category"]), Convert.ToString(row["description"]), Convert.ToString(row["tags"]), Convert.ToString(row["floor"]),
                    Convert.ToString(row["landscape"]), ToBool(row["allow_pets"]), ToBool(row["allow_pets_eat"]), ToBool(row["room_blocking_disabled"]),
                    ToBool(row["allow_hidewall"]),
                    ToInt(row["wallthick"]), ToInt(row["floorthick"]), Convert.ToString(row["wallpaper"]), ToInt(row["mute_settings"], 1),
                    ToInt(row["ban_settings"], 1),
                    ToInt(row["kick_settings"], 1), ToInt(row["chat_mode"]), ToInt(row["chat_size"]), ToInt(row["chat_speed"]),
                    ToInt(row["chat_extra_flood"]),
                    ToInt(row["chat_hearing_distance"], 100), ToInt(row["trade_settings"]), ToBool(row["push_enabled"]), ToBool(row["pull_enabled"]),
                    ToBool(row["spush_enabled"]), ToBool(row["spull_enabled"]), ToBool(row["enables_enabled"]),
                    ToBool(row["respect_notifications_enabled"]),
                    ToBool(row["pet_morphs_allowed"]), ToInt(row["group_id"]), ToInt(row["sale_price"]), ToBool(row["lay_enabled"]), model);
                data.IsSafeZone = ToBool(row["is_safe_zone"]);
                data.CorporationId = Convert.ToInt32(row["corporation_id"]);
                data.AllowMedical = ToBool(row["allow_medical"]);
                data.AllowPolice = ToBool(row["allow_police"]);
                data.AllowStaff = ToBool(row["allow_staff"]);
                return true;
            }
        }
        data = null;
        return false;
    }
}