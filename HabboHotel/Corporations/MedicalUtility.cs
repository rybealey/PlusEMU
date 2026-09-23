using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: who is allowed to act as a paramedic.
///
/// The medical counterpart to <see cref="PoliceUtility"/>, and deliberately
/// the same shape - with one difference that matters. Police powers are a job
/// and nothing more: any rank on the force may stun, cuff and escort. Medical
/// transport is a job AND a rank, because carrying an unconscious player off
/// the street is the sharp end of the hospital's work and a first-day nurse
/// has no business doing it.
///
/// The rank half is already recorded: `rp_corporation_ranks.emergency_eligible`
/// is set for HMMC Paramedic and above by 55_EmergencyEligibility.sql, which is
/// the same flag that decides who may clock in at an emergency scene. Reusing
/// it keeps "senior enough to respond" in ONE place rather than growing a
/// second, quietly divergent definition of the same seniority.
///
/// Read fresh on every use rather than cached, for the reason PoliceUtility
/// gives: a cache goes stale the moment someone clocks off, which is exactly
/// the moment it matters.
/// </summary>
public static class MedicalUtility
{
    /// <summary>
    /// On the hospital's books at a rank cleared for emergency work, AND
    /// clocked in right now. All three halves are required.
    /// </summary>
    public static bool IsOnDutyParamedic(int userId)
    {
        if (userId <= 0)
            return false;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT 1 FROM `rp_corporation_employees` e " +
            "JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`user_id` = @id AND e.`on_duty` = 1 " +
            "AND c.`service_type` = 'medical' AND r.`emergency_eligible` = 1 LIMIT 1");
        dbClient.AddParameter("id", userId);
        return dbClient.GetRow() != null;
    }

    /// <summary>
    /// Cleared for emergency work, on duty or not. The off-duty half of
    /// <see cref="IsOnDutyParamedic"/>, so a paramedic at home is told to clock
    /// in rather than told they are not a paramedic.
    /// </summary>
    public static bool IsParamedic(int userId)
    {
        if (userId <= 0)
            return false;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT 1 FROM `rp_corporation_employees` e " +
            "JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`user_id` = @id " +
            "AND c.`service_type` = 'medical' AND r.`emergency_eligible` = 1 LIMIT 1");
        dbClient.AddParameter("id", userId);
        return dbClient.GetRow() != null;
    }

    /// <summary>
    /// On the hospital's books at ANY rank and clocked in right now.
    ///
    /// The gate for selling, which is a counter job rather than an emergency
    /// one: a receptionist may sell a medkit without being cleared to drive an
    /// ambulance. What it does NOT drop is the clock - a sale is the hospital
    /// trading, and the hospital is only trading while somebody is at work.
    /// </summary>
    public static bool IsOnDutyHospitalStaff(int userId)
    {
        if (userId <= 0)
            return false;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT 1 FROM `rp_corporation_employees` e " +
            "JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "WHERE e.`user_id` = @id AND c.`service_type` = 'medical' AND e.`on_duty` = 1 LIMIT 1");
        dbClient.AddParameter("id", userId);
        return dbClient.GetRow() != null;
    }

    /// <summary>
    /// Employed at the hospital at ALL, at any rank. Only used to tell a junior
    /// medic apart from a civilian in the refusal: "you are not qualified yet"
    /// is useful to a nurse and meaningless to a shopkeeper.
    /// </summary>
    public static bool IsHospitalStaff(int userId)
    {
        if (userId <= 0)
            return false;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT 1 FROM `rp_corporation_employees` e " +
            "JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "WHERE e.`user_id` = @id AND c.`service_type` = 'medical' LIMIT 1");
        dbClient.AddParameter("id", userId);
        return dbClient.GetRow() != null;
    }

    /// <summary>
    /// The refusal wording, splitting three cases apart the way
    /// <see cref="PoliceUtility.RequireOnDuty"/> splits two: a civilian, a
    /// hospital employee who has not made Paramedic, and a paramedic who is
    /// simply off the clock. Each one needs a different thing to happen next.
    /// </summary>
    public static bool RequireOnDutyParamedic(GameClient session, string verb)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return false;
        if (IsOnDutyParamedic(habbo.Id))
            return true;
        if (IsParamedic(habbo.Id))
            session.SendWhisper($"You have to be on duty to {verb}. Clock in from the Corporations drawer.");
        else if (IsHospitalStaff(habbo.Id))
            session.SendWhisper($"You are not qualified to {verb} - that takes a Paramedic.");
        else
            session.SendWhisper($"Only paramedics can {verb}.");
        return false;
    }
}
