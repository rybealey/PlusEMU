using Plus.HabboHotel.Gangs;

namespace Plus.HabboHotel.CityPanel;

/// <summary>One row of the City Panel's Rooms &amp; Zones list.</summary>
public sealed class CityRoomRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    /// <summary>0 unsafe, 1 safe, 2 turf - RpRoomZoneTypeSaveEvent's numbers.</summary>
    public int Zone { get; init; }
    public string TurfHolder { get; init; } = "";
    public bool JailRoom { get; init; }
    public int ArrestPoints { get; init; }
    public int UsersNow { get; init; }
    public int UsersMax { get; init; }
}

/// <summary>
/// The Rooms &amp; Zones tab: every room at a glance - its zone, who holds it if
/// it is a turf, what the police have set up there, and who is in it. Editing
/// stays in the Room Tool, which already does it; this list opens it.
/// </summary>
public static class CityRooms
{
    public const int FilterAll = 0;
    public const int FilterTurf = 1;
    public const int FilterPolice = 2;
    public const int FilterOccupied = 3;

    private const int Limit = 100;

    // An Arrest Point is furni: the behaviour on the furni's type, or scoped to
    // one placed piece (rp_item_function) - the override wins, as in the room.
    private const string ArrestPointJoin =
        "FROM `items` i JOIN `furniture` f ON f.`id` = i.`base_item` " +
        "LEFT JOIN `rp_item_function` o ON o.`item_id` = i.`id` AND o.`field` = 'interaction_type' " +
        "WHERE COALESCE(NULLIF(o.`value`, ''), f.`interaction_type`) = 'arrest_point'";

    public static List<CityRoomRow> List(string query, int filter)
    {
        query = (query ?? "").Replace("%", "").Replace("_", "").Trim();
        if (query.Length > 50)
            query = query[..50];

        var where = new List<string>();
        if (query.Length > 0)
            where.Add(int.TryParse(query, out _) ? "(r.`id` = @id OR r.`caption` LIKE @query)" : "r.`caption` LIKE @query");
        switch (filter)
        {
            case FilterTurf:
                where.Add("r.`is_turf` = '1'");
                break;
            case FilterPolice:
                where.Add("(r.`rp_jail_room` = '1' OR r.`id` IN (SELECT i.`room_id` " + ArrestPointJoin + "))");
                break;
            case FilterOccupied:
                where.Add("r.`users_now` > 0");
                break;
        }

        var rows = new List<CityRoomRow>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        // The enum('0','1') flags are compared in SQL, so they come back as
        // numbers rather than strings (or, for a TINYINT(1), a bool).
        dbClient.SetQuery(
            "SELECT r.`id`, r.`caption`, (r.`is_safe_zone` = '1') AS safe, (r.`is_turf` = '1') AS turf, " +
            "(r.`rp_jail_room` = '1') AS jail, r.`users_now`, r.`users_max` FROM `rooms` r" +
            (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "") +
            " ORDER BY r.`users_now` DESC, r.`caption` LIMIT " + Limit);
        if (query.Length > 0)
        {
            dbClient.AddParameter("query", "%" + query + "%");
            if (int.TryParse(query, out var id))
                dbClient.AddParameter("id", id);
        }
        var table = dbClient.GetTable();
        if (table == null || table.Rows.Count == 0)
            return rows;

        var ids = table.Rows.Cast<System.Data.DataRow>().Select(row => Convert.ToInt32(row["id"])).ToList();
        var arrestPoints = new Dictionary<int, int>();
        dbClient.SetQuery("SELECT i.`room_id`, COUNT(*) AS points " + ArrestPointJoin +
                          " AND i.`room_id` IN (" + string.Join(",", ids) + ") GROUP BY i.`room_id`");
        var points = dbClient.GetTable();
        if (points != null)
            foreach (System.Data.DataRow row in points.Rows)
                arrestPoints[Convert.ToInt32(row["room_id"])] = Convert.ToInt32(row["points"]);

        foreach (System.Data.DataRow row in table.Rows)
        {
            var id = Convert.ToInt32(row["id"]);
            var turf = Convert.ToInt32(row["turf"]) == 1;
            rows.Add(new CityRoomRow
            {
                Id = id,
                Name = Convert.ToString(row["caption"]) ?? "",
                Zone = turf ? 2 : (Convert.ToInt32(row["safe"]) == 1 ? 1 : 0),
                TurfHolder = turf ? (TurfManager.OwnerName((uint)id) ?? "") : "",
                JailRoom = Convert.ToInt32(row["jail"]) == 1,
                ArrestPoints = arrestPoints.GetValueOrDefault(id),
                UsersNow = Convert.ToInt32(row["users_now"]),
                UsersMax = Convert.ToInt32(row["users_max"])
            });
        }
        return rows;
    }
}
