using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the Corporations window opened - send the corporations directory.
/// Viewable by every player (no staff gate; corporations are the economy's
/// front door).
/// </summary>
internal class RpGetCorpsEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null)
            return Task.CompletedTask;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var corps = connection.Query<(int Id, string Name, string Badge, int Employees)>(
            "SELECT c.`id`, c.`name`, c.`badge`, " +
            "(SELECT COUNT(*) FROM `rp_corporation_employees` e WHERE e.`corporation_id` = c.`id`) AS employees " +
            "FROM `rp_corporations` c ORDER BY c.`sort_order`, c.`id`")
            .Select(row => new RpCorpsComposer.CorpEntry(row.Id, row.Name, row.Badge, row.Employees))
            .ToList();
        session.Send(new RpCorpsComposer(corps));
        return Task.CompletedTask;
    }
}
