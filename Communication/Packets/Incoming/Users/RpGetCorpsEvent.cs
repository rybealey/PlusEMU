using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the Corporations window opened - send the corporations directory.
/// Viewable by every player (no staff gate; corporations are the economy's
/// front door).
///
/// The headcount leaves out staff who have hidden themselves, for the same
/// reason the roster does: a directory reading 12 beside a roster listing 11
/// says there is a twelfth just as plainly as naming them would.
/// </summary>
internal class RpGetCorpsEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null)
            return Task.CompletedTask;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var viewerId = session.GetHabbo().Id;
        var seeHidden = StaffVisibility.CanSeeHiddenStaff(session) ? 1 : 0;
        var corps = connection.Query<(int Id, string Name, string Badge, int Employees)>(
            "SELECT c.`id`, c.`name`, c.`badge`, " +
            "(SELECT COUNT(*) FROM `rp_corporation_employees` e " +
            " INNER JOIN `users` u ON u.`id` = e.`user_id` " +
            " WHERE e.`corporation_id` = c.`id` " +
            " AND (@seeHidden = 1 OR IFNULL(u.`hidden_staff`, 0) = 0 OR u.`id` = @viewerId)) AS employees " +
            "FROM `rp_corporations` c ORDER BY c.`sort_order`, c.`id`", new { seeHidden, viewerId })
            .Select(row => new RpCorpsComposer.CorpEntry(row.Id, row.Name, row.Badge, row.Employees))
            .ToList();
        session.Send(new RpCorpsComposer(corps));
        return Task.CompletedTask;
    }
}
