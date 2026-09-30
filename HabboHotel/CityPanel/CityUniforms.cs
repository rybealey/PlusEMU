using Dapper;
using Plus.HabboHotel.Corporations;

namespace Plus.HabboHotel.CityPanel;

/// <summary>A rank in the Uniforms tab's wearer list, and whether it has a uniform for each gender.</summary>
public sealed class CityUniformRank
{
    public int Id { get; set; }
    public int CorporationId { get; set; }
    public int RankOrder { get; set; }
    public string Name { get; set; } = "";
    public bool HasMale { get; set; }
    public bool HasFemale { get; set; }
}

public sealed class CityUniformCorp
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<CityUniformRank> Ranks { get; } = new();
}

/// <summary>
/// The Uniforms tab's wearers: every corporation with its ranks (highest
/// first), and the prisoners. The outfits themselves are UniformManager's.
/// </summary>
public static class CityUniforms
{
    public static List<CityUniformCorp> Corporations()
    {
        List<CityUniformCorp> corps;
        List<CityUniformRank> ranks;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            corps = connection.Query<CityUniformCorp>("SELECT `id` AS Id, `name` AS Name FROM `rp_corporations` ORDER BY `sort_order`, `id`").ToList();
            ranks = connection.Query<CityUniformRank>(
                "SELECT `id` AS Id, `corporation_id` AS CorporationId, `rank_order` AS RankOrder, `name` AS Name " +
                "FROM `rp_corporation_ranks` ORDER BY `rank_order` DESC").ToList();
        }
        var defined = UniformManager.Defined().Where(key => key.Kind == UniformManager.KindRank).ToHashSet();
        foreach (var rank in ranks)
        {
            rank.HasMale = defined.Contains((UniformManager.KindRank, rank.Id, "M"));
            rank.HasFemale = defined.Contains((UniformManager.KindRank, rank.Id, "F"));
            corps.FirstOrDefault(corp => corp.Id == rank.CorporationId)?.Ranks.Add(rank);
        }
        return corps;
    }

    public static (bool Male, bool Female) Prisoner() =>
        (UniformManager.Get(UniformManager.KindPrisoner, 0, "M").Length > 0, UniformManager.Get(UniformManager.KindPrisoner, 0, "F").Length > 0);
}
