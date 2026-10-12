using Dapper;

namespace Plus.HabboHotel.CityPanel;

/// <summary>
/// pixelrp City Panel: the Economy tab's Trade History - every completed
/// backpack trade (rp_trades, migration 244), newest first. A search names
/// players (a username prefix, resolved to ids first); fifty to a page.
/// </summary>
public static class CityTrades
{
    public const int PageSize = 50;

    public sealed class Row
    {
        public int Id { get; set; }
        public int CreatedAt { get; set; }
        public int UserA { get; set; }
        public string NameA { get; set; } = "";
        public int UserB { get; set; }
        public string NameB { get; set; } = "";
        public string RoomName { get; set; } = "";
        public string GaveA { get; set; } = "";
        public string GaveB { get; set; } = "";
    }

    public static (List<Row> Rows, bool More) Page(string query, int offset)
    {
        offset = Math.Max(0, offset);
        query = (query ?? "").Trim();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        List<int>? ids = null;
        if (query.Length > 0)
        {
            var like = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            ids = connection.Query<int>("SELECT `id` FROM `users` WHERE `username` LIKE @like AND `rp_system_account` = 0 ORDER BY `username` LIMIT 50", new { like }).ToList();
            if (ids.Count == 0)
                return (new List<Row>(), false);
        }
        var who = ids == null ? "" : " WHERE `user_a` IN @ids OR `user_b` IN @ids";
        var rows = connection.Query<Row>(
            "SELECT `id` AS Id, `created_at` AS CreatedAt, `user_a` AS UserA, `name_a` AS NameA, `user_b` AS UserB, `name_b` AS NameB, " +
            "`room_name` AS RoomName, `gave_a` AS GaveA, `gave_b` AS GaveB FROM `rp_trades`" + who +
            " ORDER BY `id` DESC LIMIT @limit OFFSET @offset",
            new { ids, limit = PageSize + 1, offset }).ToList();
        var more = rows.Count > PageSize;
        if (more)
            rows.RemoveAt(rows.Count - 1);
        return (rows, more);
    }
}
