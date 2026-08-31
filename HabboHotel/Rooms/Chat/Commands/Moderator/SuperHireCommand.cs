using Dapper;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: staff-only hire into any corporation, rank and tier:
/// :superhire &lt;username&gt; &lt;ACRONYM&gt; [rank] [tier]
/// Rank and tier are numbers (rank 1 = lowest; tier 1-5). With only an
/// acronym, hires into the lowest rank at tier 1. One job per player - a
/// superhire replaces any existing employment. The target does NOT need to
/// be online; announcements simply skip an offline player.
/// </summary>
internal class SuperHireCommand : IChatCommand
{
    public string Key => "superhire";
    public string PermissionRequired => "command_superhire";

    public string Parameters => "%username% %acronym% [rank] [tier]";

    public string Description => "Hire a player into a corporation at any rank and tier.";

    // Tiers are entered as numbers but always read as numerals: Cadet II.
    // Tier 0 = a no-tier leadership rank; renders as nothing.
    private static string TierNumeral(int tier) => tier switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V",
        _ => ""
    };

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length < 2)
        {
            session.SendWhisper("Usage: :superhire <username> <acronym> [rank] [tier]");
            return;
        }
        var target = CorporationUtility.ResolveUser(parameters[0]);
        if (target == null)
        {
            session.SendWhisper($"No player named '{parameters[0]}'.");
            return;
        }
        var acronym = parameters[1].ToUpperInvariant();
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var corp = connection.QuerySingleOrDefault<(int Id, string Name)>(
            "SELECT `id`, `name` FROM `rp_corporations` WHERE UPPER(`acronym`) = @acronym AND `acronym` != '' LIMIT 1", new { acronym });
        if (corp.Id == 0)
        {
            var keys = connection.Query<string>("SELECT `acronym` FROM `rp_corporations` WHERE `acronym` != '' ORDER BY `sort_order`, `id`").ToList();
            session.SendWhisper($"Unknown corporation '{parameters[1]}'. Available: {string.Join(", ", keys)}");
            return;
        }
        var rankOrder = 1;
        if (parameters.Length >= 3 && !int.TryParse(parameters[2], out rankOrder))
        {
            session.SendWhisper("Rank must be a number (1 = lowest rank).");
            return;
        }
        var rank = connection.QuerySingleOrDefault<(int Id, string Name, int Tiers, int MaxOrder)>(
            "SELECT `id`, `name`, `tiers`, (SELECT MAX(`rank_order`) FROM `rp_corporation_ranks` WHERE `corporation_id` = @corpId) AS MaxOrder " +
            "FROM `rp_corporation_ranks` WHERE `corporation_id` = @corpId AND `rank_order` = @rankOrder LIMIT 1",
            new { corpId = corp.Id, rankOrder });
        if (rank.Id == 0)
        {
            session.SendWhisper($"{corp.Name} has no rank {rankOrder} (ranks run 1-{rank.MaxOrder}).");
            return;
        }
        var tier = 1;
        if (rank.Tiers == 0)
        {
            // leadership ranks carry no tiers
            if (parameters.Length >= 4)
            {
                session.SendWhisper($"{rank.Name} is a leadership rank and has no tiers.");
                return;
            }
            tier = 0;
        }
        else
        {
            if (parameters.Length >= 4 && !int.TryParse(parameters[3], out tier))
            {
                session.SendWhisper($"Tier must be a number (1-{rank.Tiers}).");
                return;
            }
            if (tier < 1 || tier > rank.Tiers)
            {
                session.SendWhisper($"Tier must be between 1 and {rank.Tiers} for {rank.Name}.");
                return;
            }
        }
        connection.Execute(
            "INSERT INTO `rp_corporation_employees` (`user_id`, `corporation_id`, `rank_id`, `tier`, `hired_at`) " +
            "VALUES (@userId, @corpId, @rankId, @tier, UNIX_TIMESTAMP()) " +
            "ON DUPLICATE KEY UPDATE `corporation_id` = @corpId, `rank_id` = @rankId, `tier` = @tier, `hired_at` = UNIX_TIMESTAMP()",
            new { userId = target.Id, corpId = corp.Id, rankId = rank.Id, tier });

        // Real-time updates: hotel-wide broadcast (corp windows, profiles,
        // infostands everywhere) + live-shift wage/motto refresh.
        CorporationUtility.BroadcastEmployment(target.Id);

        // Announce theatrically: the staff member shouts the hire in their
        // room (bubble 23) and an online new hire shouts in theirs (bubble 4).
        var title = string.IsNullOrEmpty(TierNumeral(tier)) ? rank.Name : $"{rank.Name} {TierNumeral(tier)}";
        var staffRoomUser = session.GetHabbo().CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(session.GetHabbo().Id);
        staffRoomUser?.OnChat(23, $"*has hired {target.Username} into {corp.Name} as {title}*", true);
        var targetRoomUser = target.Client?.GetHabbo()?.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(target.Id);
        if (targetRoomUser != null)
            targetRoomUser.OnChat(4, $"*has been hired into {corp.Name} as {title}*", true);
        else
            target.Client?.SendWhisper($"You've been hired into {corp.Name} as {title}!");
    }
}
