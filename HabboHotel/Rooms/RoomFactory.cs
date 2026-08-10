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
                if (PlusEnvironment.Game.RoomManager.TryGetRoom(Convert.ToUInt32(row["id"]), out var room))
                    data.Add(room);
                else
                {
                    if (!PlusEnvironment.Game.RoomManager.TryGetModel(Convert.ToString(row["model_name"]), out var model)) continue;
                    data.Add(new(Convert.ToUInt32(row["id"]), Convert.ToString(row["caption"]), Convert.ToString(row["model_name"]), Convert.ToString(row["username"]),
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
                        ToBool(row["pet_morphs_allowed"]), ToInt(row["group_id"]), ToInt(row["sale_price"]), ToBool(row["lay_enabled"]), model));
                }
            }
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
                return true;
            }
        }
        data = null;
        return false;
    }
}