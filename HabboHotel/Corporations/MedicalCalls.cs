using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: the hospital's emergency queue - 911 Dispatch's counterpart for
/// paramedics (EmergencyCalls).
///
/// Nobody calls it: every time a player goes down (0 health, from
/// RoomUserManager.ApplyRpKnockout) the queue gets an entry - who went down and
/// the room they went down in - and every on-duty paramedic and above (the
/// ranks <see cref="MedicalUtility.IsOnDutyParamedic"/> clears for emergency
/// work) is sent it. The window opens on clock-in and closes on clock-out
/// (PushWindow, from PoliceUtility.PushPardonRights, which runs at exactly
/// those moments). Its X only puts it away: the next death brings it back up,
/// and so does :calls.
///
/// A report, not a call: there is nothing to press (2026-10-16) - no
/// Respond, no Go to room, no Helpful or Abuse. The paramedic reads where the
/// patient is and goes there themselves.
///
/// Held in memory, like HospitalAdmission's clock and for its reason: being
/// down is a moment, and after a few minutes the hospital collects the patient
/// itself, so a death from before a restart is no use to anybody.
/// </summary>
public static class MedicalCalls
{
    /// <summary>How many of the newest deaths the window holds.</summary>
    public const int WindowSize = 20;

    public sealed class Death
    {
        public int Id { get; init; }
        public int PatientId { get; init; }
        public string PatientName { get; init; } = "";
        public string PatientLook { get; init; } = "";
        public string PatientGender { get; init; } = "M";
        public int RoomId { get; init; }
        public string RoomName { get; init; } = "";
        public int CreatedAt { get; init; }
    }

    private static readonly object Lock = new();

    /// <summary>Newest first.</summary>
    private static readonly List<Death> Deaths = new();

    private static int _nextId;

    /// <summary>The window's deaths, newest first.</summary>
    public static List<Death> Newest()
    {
        lock (Lock)
            return Deaths.ToList();
    }

    /// <summary>A player has just gone down in `room`. Report it.</summary>
    public static void OnDeath(Room room, RoomUser user)
    {
        var habbo = user?.GetClient()?.GetHabbo();
        if (room == null || user == null || user.IsBot || habbo == null)
            return;

        lock (Lock)
        {
            Deaths.Insert(0, new Death
            {
                Id = ++_nextId,
                PatientId = habbo.Id,
                PatientName = habbo.Username,
                PatientLook = habbo.Look ?? "",
                PatientGender = (habbo.Gender ?? "M").ToUpperInvariant().StartsWith("F") ? "F" : "M",
                RoomId = (int)room.Id,
                RoomName = room.Name ?? "",
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            if (Deaths.Count > WindowSize)
                Deaths.RemoveRange(WindowSize, Deaths.Count - WindowSize);
        }

        Broadcast();
    }

    /// <summary>
    /// Open or close one player's window: open with the deaths while they are
    /// an on-duty paramedic, closed otherwise.
    /// </summary>
    public static void PushWindow(GameClient? session, bool onDuty)
    {
        if (session?.GetHabbo() == null)
            return;
        session.Send(onDuty ? new RpMedicalCallsComposer(true, Newest()) : new RpMedicalCallsComposer(false, new List<Death>()));
    }

    /// <summary>
    /// :calls - the window back up for an on-duty paramedic, even if they put it
    /// away with its X. The caller has checked the duty.
    /// </summary>
    public static void Reopen(GameClient session) =>
        session.Send(new RpMedicalCallsComposer(true, Newest(), reopen: true));

    /// <summary>The queue to every on-duty paramedic and above.</summary>
    private static void Broadcast()
    {
        var deaths = Newest();
        foreach (var userId in OnDutyParamedics())
            PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.Send(new RpMedicalCallsComposer(true, deaths));
    }

    /// <summary>
    /// The hospital's on-duty staff at a rank cleared for emergency work - the
    /// same three halves as <see cref="MedicalUtility.IsOnDutyParamedic"/>, for
    /// everybody at once.
    /// </summary>
    private static List<int> OnDutyParamedics()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<int>(
            "SELECT e.`user_id` FROM `rp_corporation_employees` e " +
            "JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
            "WHERE e.`on_duty` = 1 AND c.`service_type` = 'medical' AND r.`emergency_eligible` = 1").Distinct().ToList();
    }
}
