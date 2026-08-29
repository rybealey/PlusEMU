using Dapper;
using Plus.Communication.Packets.Outgoing.Users;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: employment lookups shared by room entry, profile opens and the
/// hire commands. Corporations change rarely; the queries are cheap joins.
/// </summary>
public static class CorporationUtility
{
    public record Employment(int UserId, int CorpId, string Badge, string CorpName, string RankName, int Tier);

    public static Employment GetEmployment(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QuerySingleOrDefault<Employment>(
            "SELECT e.`user_id` AS UserId, c.`id` AS CorpId, c.`badge` AS Badge, c.`name` AS CorpName, r.`name` AS RankName, e.`tier` AS Tier " +
            "FROM `rp_corporation_employees` e " +
            "INNER JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`user_id` = @userId LIMIT 1", new { userId });
    }

    public static List<Employment> GetEmployments(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (!ids.Any())
            return new List<Employment>();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<Employment>(
            "SELECT e.`user_id` AS UserId, c.`id` AS CorpId, c.`badge` AS Badge, c.`name` AS CorpName, r.`name` AS RankName, e.`tier` AS Tier " +
            "FROM `rp_corporation_employees` e " +
            "INNER JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "INNER JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`user_id` IN @ids", new { ids }).ToList();
    }

    public static RpUserCorpComposer ComposeFor(int userId, Employment employment)
    {
        if (employment == null || employment.CorpId == 0)
            return new RpUserCorpComposer(userId, 0, "", "", "", 0);
        return new RpUserCorpComposer(employment.UserId, employment.CorpId, employment.Badge, employment.CorpName, employment.RankName, employment.Tier);
    }
}
