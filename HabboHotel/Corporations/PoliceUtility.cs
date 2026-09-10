using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: who is allowed to act as police.
///
/// Police powers are a JOB, not a rank: :stun, :cuff and :escort are open to
/// an employee of a corporation flagged `is_police` who is clocked in, and to
/// nobody else. Both halves matter - employment says you are an officer, and
/// on-duty says you are one right now. An officer at home in plain clothes has
/// no more authority than anyone else.
///
/// Read fresh on every use rather than cached. It is one indexed lookup on a
/// two-row-per-player table, and the alternative is a cache that goes stale
/// the moment someone clocks off, which is exactly the moment it matters.
/// </summary>
public static class PoliceUtility
{
    public static bool IsOnDutyOfficer(int userId)
    {
        if (userId <= 0)
            return false;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT 1 FROM `rp_corporation_employees` e " +
            "JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "WHERE e.`user_id` = @id AND e.`on_duty` = 1 AND c.`is_police` = 1 LIMIT 1");
        dbClient.AddParameter("id", userId);
        return dbClient.GetRow() != null;
    }

    /// <summary>
    /// Employed by the force at all, on duty or not. What a charge needs to
    /// stay on the sheet when the officer who filed it clocks off.
    /// </summary>
    public static bool IsOfficer(int userId)
    {
        if (userId <= 0)
            return false;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT 1 FROM `rp_corporation_employees` e " +
            "JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "WHERE e.`user_id` = @id AND c.`is_police` = 1 LIMIT 1");
        dbClient.AddParameter("id", userId);
        return dbClient.GetRow() != null;
    }

    /// <summary>
    /// The one refusal wording every police command shares, so a civilian and
    /// an off-duty officer are told apart - "you are not police" to someone
    /// who never was, and "clock in" to someone who only has to.
    /// </summary>
    /// <summary>
    /// Tell one client whether it may drop charges from the Wanted list, which
    /// is exactly "are you an on-duty officer". Sent at login and on every
    /// clock-in and clock-off - the only moments the answer changes - because
    /// the wanted list itself is one broadcast and cannot carry a per-player
    /// answer. Gates the affordance only; the drop packet re-checks.
    /// </summary>
    public static void PushPardonRights(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        session.Send(new Communication.Packets.Outgoing.Users.RpPoliceComposer(IsOnDutyOfficer(habbo.Id)));
    }

    public static bool RequireOnDuty(GameClient session, string verb)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return false;
        if (IsOnDutyOfficer(habbo.Id))
            return true;
        session.SendWhisper(IsOfficer(habbo.Id)
            ? $"You have to be on duty to {verb}. Clock in from the Corporations drawer."
            : $"Only police officers can {verb}.");
        return false;
    }
}
