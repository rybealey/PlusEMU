using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Users.Birthdays;

/// <summary>pixelrp: month/day birthday storage behind the phone's Account screen.</summary>
public static class BirthdayUtility
{
    private static readonly int[] DaysInMonth = { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    public static bool IsValid(int month, int day) => month >= 1 && month <= 12 && day >= 1 && day <= DaysInMonth[month - 1];

    public static void SendBirthday(GameClient session)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        (int Month, int Day)? row;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            row = connection.QueryFirstOrDefault<(int Month, int Day)?>(
                "SELECT `month` AS Month, `day` AS Day FROM `rp_user_birthdays` WHERE `user_id` = @userId LIMIT 1", new { userId = habbo.Id });
        session.Send(new RpBirthdayComposer(row?.Month ?? 0, row?.Day ?? 0));
    }
}
