using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Birthdays;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: save (month, day) or remove (0, 0) the player's birthday. Anything
/// invalid is ignored and the stored value is sent back unchanged.
/// </summary>
internal class RpSaveBirthdayEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var month = packet.ReadInt();
        var day = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        if (month == 0 && day == 0)
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Execute("DELETE FROM `rp_user_birthdays` WHERE `user_id` = @userId", new { userId = habbo.Id });
        }
        else if (BirthdayUtility.IsValid(month, day))
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Execute(
                "INSERT INTO `rp_user_birthdays` (`user_id`, `month`, `day`) VALUES (@userId, @month, @day) " +
                "ON DUPLICATE KEY UPDATE `month` = VALUES(`month`), `day` = VALUES(`day`)", new { userId = habbo.Id, month, day });
        }

        BirthdayUtility.SendBirthday(session, habbo.Id);
        return Task.CompletedTask;
    }
}
