using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Users.Birthdays;

/// <summary>
/// pixelrp: month/day birthday storage behind the phone's Account screen.
/// Read by the profile window (any player's) and by login for the greeting.
/// </summary>
public static class BirthdayUtility
{
    private static readonly int[] DaysInMonth = { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    public static bool IsValid(int month, int day) => month >= 1 && month <= 12 && day >= 1 && day <= DaysInMonth[month - 1];

    public static (int Month, int Day) GetBirthday(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var row = connection.QueryFirstOrDefault<(int Month, int Day)?>(
            "SELECT `month` AS Month, `day` AS Day FROM `rp_user_birthdays` WHERE `user_id` = @userId LIMIT 1", new { userId });
        return row ?? (0, 0);
    }

    /// <summary>Today, by the server's clock. 29 February counts on 1 March in non-leap years.</summary>
    public static bool IsBirthdayToday(int month, int day)
    {
        if (month == 0) return false;
        var today = DateTime.Now;
        if (today.Month == month && today.Day == day) return true;
        return month == 2 && day == 29 && !DateTime.IsLeapYear(today.Year) && today.Month == 3 && today.Day == 1;
    }

    public static void SendBirthday(GameClient session, int userId)
    {
        if (session.GetHabbo() == null || userId <= 0)
            return;
        var birthday = GetBirthday(userId);
        session.Send(new RpBirthdayComposer(userId, birthday.Month, birthday.Day));
    }
}
