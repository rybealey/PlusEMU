using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: one corporation's full roster for the Corporations window - the
/// rank ladder (with pay per 10-minute shift interval and tier ceilings) and
/// the employees at each rank.
/// </summary>
internal class RpGetCorpDetailEvent : IPacketEvent
{
    private readonly IGameClientManager _clientManager;

    public RpGetCorpDetailEvent(IGameClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var corpId = packet.ReadInt();
        if (session.GetHabbo() == null)
            return Task.CompletedTask;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var corp = connection.QuerySingleOrDefault<(int Id, string Name, string Badge, string Description, int Stock)>(
            "SELECT `id`, `name`, `badge`, `description`, `stock` FROM `rp_corporations` WHERE `id` = @corpId LIMIT 1",
            new { corpId });
        if (corp.Id == 0)
            return Task.CompletedTask;
        var ranks = connection.Query<(int Id, int RankOrder, string Name, int Pay, int Tiers)>(
            "SELECT `id`, `rank_order` AS RankOrder, `name`, `pay`, `tiers` FROM `rp_corporation_ranks` " +
            "WHERE `corporation_id` = @corpId ORDER BY `rank_order`", new { corpId }).ToList();
        var employees = connection.Query<(int UserId, int RankId, int Tier, int OnDuty, string Username, string Figure)>(
            "SELECT e.`user_id` AS UserId, e.`rank_id` AS RankId, e.`tier`, e.`on_duty` AS OnDuty, u.`username`, u.`look` AS Figure " +
            "FROM `rp_corporation_employees` e INNER JOIN `users` u ON u.`id` = e.`user_id` " +
            "WHERE e.`corporation_id` = @corpId ORDER BY e.`tier` DESC, u.`username`", new { corpId }).ToList();
        var rankPayload = ranks.Select(rank => new RpCorpDetailComposer.Rank(
            rank.Id, rank.RankOrder, rank.Name, rank.Pay, rank.Tiers,
            employees.Where(employee => employee.RankId == rank.Id)
                .Select(employee => new RpCorpDetailComposer.Employee(
                    employee.Username, employee.Figure, employee.Tier,
                    _clientManager.GetClientByUserId(employee.UserId) != null,
                    employee.OnDuty == 1))
                .ToList()))
            .ToList();
        session.Send(new RpCorpDetailComposer(corp.Id, corp.Name, corp.Badge, corp.Description, corp.Stock, rankPayload));
        return Task.CompletedTask;
    }
}
