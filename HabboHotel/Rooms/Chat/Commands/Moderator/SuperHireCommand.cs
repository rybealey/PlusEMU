using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: staff-only hire into any corporation, rank and tier:
/// :superhire &lt;username&gt; &lt;corporation-key&gt; [rank] [tier]
/// Rank and tier are numbers (rank 1 = lowest; tier 1-5). With only a
/// corporation key, hires into the lowest rank at tier 1. One job per
/// player - a superhire replaces any existing employment. The room gets a
/// real-time RpUserCorpComposer so infostands and open profiles update.
/// </summary>
internal class SuperHireCommand : ITargetChatCommand
{
    public string Key => "superhire";
    public string PermissionRequired => "command_superhire";

    public string Parameters => "%username% %corporation% [rank] [tier]";

    public string Description => "Hire a player into a corporation at any rank and tier.";

    public bool MustBeInSameRoom => false;

    // Tiers are entered as numbers but always read as numerals: Cadet II.
    private static string TierNumeral(int tier) => tier switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V",
        _ => tier.ToString()
    };

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (parameters.Length < 1 || string.IsNullOrWhiteSpace(parameters[0]))
        {
            session.SendWhisper("Usage: :superhire <username> <corporation> [rank] [tier]");
            return Task.CompletedTask;
        }
        var corpKey = parameters[0].ToLowerInvariant();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var corp = connection.QuerySingleOrDefault<(int Id, string Name)>(
            "SELECT `id`, `name` FROM `rp_corporations` WHERE `corp_key` = @corpKey AND `corp_key` != '' LIMIT 1", new { corpKey });
        if (corp.Id == 0)
        {
            var keys = connection.Query<string>("SELECT `corp_key` FROM `rp_corporations` WHERE `corp_key` != '' ORDER BY `sort_order`, `id`").ToList();
            session.SendWhisper($"Unknown corporation '{corpKey}'. Available: {string.Join(", ", keys)}");
            return Task.CompletedTask;
        }
        var rankOrder = 1;
        if (parameters.Length >= 2 && !int.TryParse(parameters[1], out rankOrder))
        {
            session.SendWhisper("Rank must be a number (1 = lowest rank).");
            return Task.CompletedTask;
        }
        var rank = connection.QuerySingleOrDefault<(int Id, string Name, int Tiers, int MaxOrder)>(
            "SELECT `id`, `name`, `tiers`, (SELECT MAX(`rank_order`) FROM `rp_corporation_ranks` WHERE `corporation_id` = @corpId) AS MaxOrder " +
            "FROM `rp_corporation_ranks` WHERE `corporation_id` = @corpId AND `rank_order` = @rankOrder LIMIT 1",
            new { corpId = corp.Id, rankOrder });
        if (rank.Id == 0)
        {
            session.SendWhisper($"{corp.Name} has no rank {rankOrder} (ranks run 1-{rank.MaxOrder}).");
            return Task.CompletedTask;
        }
        var tier = 1;
        if (parameters.Length >= 3 && !int.TryParse(parameters[2], out tier))
        {
            session.SendWhisper($"Tier must be a number (1-{rank.Tiers}).");
            return Task.CompletedTask;
        }
        if (tier < 1 || tier > rank.Tiers)
        {
            session.SendWhisper($"Tier must be between 1 and {rank.Tiers} for {rank.Name}.");
            return Task.CompletedTask;
        }
        connection.Execute(
            "INSERT INTO `rp_corporation_employees` (`user_id`, `corporation_id`, `rank_id`, `tier`, `hired_at`) " +
            "VALUES (@userId, @corpId, @rankId, @tier, UNIX_TIMESTAMP()) " +
            "ON DUPLICATE KEY UPDATE `corporation_id` = @corpId, `rank_id` = @rankId, `tier` = @tier, `hired_at` = UNIX_TIMESTAMP()",
            new { userId = target.Id, corpId = corp.Id, rankId = rank.Id, tier });

        // Real-time updates: everyone in the target's room (infostand corp
        // slot), the target themself, and the hiring staff member.
        var employment = CorporationUtility.GetEmployment(target.Id);
        var composer = CorporationUtility.ComposeFor(target.Id, employment);
        var targetRoom = target.CurrentRoom;
        if (targetRoom != null)
            targetRoom.SendPacket(composer);
        else
            target.Client?.Send(composer);
        if (session.GetHabbo().CurrentRoom?.Id != targetRoom?.Id)
            session.Send(composer);

        var tierNumeral = TierNumeral(tier);
        session.SendWhisper($"Hired {target.Username} into {corp.Name} as {rank.Name} {tierNumeral}.");
        target.Client?.SendWhisper($"You've been hired into {corp.Name} as {rank.Name} {tierNumeral}!");
        return Task.CompletedTask;
    }
}
