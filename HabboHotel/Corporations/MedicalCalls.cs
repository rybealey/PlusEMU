using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: the hospital's emergency queue, Paramedic Dispatch - 911
/// Dispatch's counterpart for paramedics (EmergencyCalls).
///
/// Nobody calls it: every time a player goes down (0 health, from
/// RoomUserManager.ApplyRpKnockout) the queue gets an entry - who went down and
/// the room they went down in. Every on-duty paramedic and above (the ranks
/// <see cref="MedicalUtility.IsOnDutyParamedic"/> clears for emergency work)
/// sees the whole queue. The window opens on clock-in and closes on clock-out
/// (PushWindow, from PoliceUtility.PushPardonRights, which runs at exactly
/// those moments); its X only puts it away, and :calls brings it back.
///
/// ROUND ROBIN (2026-10-16). A death is offered to ONE paramedic at a time,
/// whose window comes up on it with a Claim button. Unclaimed after
/// <see cref="ClaimSeconds"/> seconds, it goes to the next paramedic on duty,
/// in turn, round and round until somebody takes it or the patient no longer
/// needs one. The turn carries on from death to death rather than starting at
/// the same paramedic every time, so the work is shared out.
///
/// One claim at a time: a paramedic holding a claimed call - or a call offered
/// to them and not yet answered - is skipped, and a second death waits for the
/// next paramedic free. A claim ends when the patient is dropped off: a
/// paramedic carrying them walks onto a Paramedic dropoff pad and lays them on
/// a bed (PoliceState.TryDropOff), and the card reads "Revived by" them. A
/// claimer who clocks off, goes down or logs out lets the call go back round.
///
/// Every other way the patient stops needing a paramedic closes the call too:
/// back on their feet (a medkit, :restore), collected by the hospital itself
/// (HospitalAdmission), or gone from the hotel.
///
/// No Respond, no Go to room, no Helpful or Abuse: the paramedic reads where
/// the patient is and goes there themselves.
///
/// Held in memory, like HospitalAdmission's clock and for its reason: being
/// down is a moment, and after a few minutes the hospital collects the patient
/// itself, so a death from before a restart is no use to anybody.
/// </summary>
public static class MedicalCalls
{
    /// <summary>How many of the newest deaths the window holds.</summary>
    public const int WindowSize = 20;

    /// <summary>How long one paramedic has to claim a call before it moves on.</summary>
    public const int ClaimSeconds = 10;

    /// <summary>A claim pressed on the last beat still counts - the trip from the client takes time.</summary>
    private const double ClaimGraceSeconds = 1.0;

    private const int TickMs = 1000;

    /// <summary>Nobody free to offer it to yet.</summary>
    public const int StateWaiting = 0;
    /// <summary>Offered to <see cref="Death.ActorId"/> until <see cref="Death.OfferExpiresAt"/>.</summary>
    public const int StateOffered = 1;
    /// <summary>Claimed by <see cref="Death.ActorId"/>.</summary>
    public const int StateClaimed = 2;
    /// <summary>Dropped off at the hospital by <see cref="Death.ActorId"/>. Closed.</summary>
    public const int StateRevived = 3;
    /// <summary>Back on their feet without a drop-off. Closed.</summary>
    public const int StateRecovered = 4;
    /// <summary>Collected by the hospital itself. Closed.</summary>
    public const int StateAdmitted = 5;
    /// <summary>Logged out while down. Closed.</summary>
    public const int StateGone = 6;

    public sealed class Death
    {
        public int Id { get; init; }
        public int PatientId { get; init; }
        /// <summary>The patient's account, so none of their own characters is offered the call.</summary>
        public int PatientRoot { get; init; }
        public string PatientName { get; init; } = "";
        public string PatientLook { get; init; } = "";
        public string PatientGender { get; init; } = "M";
        public int RoomId { get; init; }
        public string RoomName { get; init; } = "";
        public int CreatedAt { get; init; }

        public int State { get; set; }
        /// <summary>Offered to, claimed by or revived by - whichever <see cref="State"/> says; 0 for none.</summary>
        public int ActorId { get; set; }
        public string ActorName { get; set; } = "";
        public DateTime OfferExpiresAt { get; set; }
        /// <summary>Whoever it was last offered to, so the turn moves on from them.</summary>
        public int LastOfferedId { get; set; }

        public bool IsOpen => State is StateWaiting or StateOffered or StateClaimed;

        public Death Copy() => (Death)MemberwiseClone();
    }

    /// <summary>One on-duty paramedic and the account they are on.</summary>
    private sealed class Medic
    {
        public int UserId { get; set; }
        public int Root { get; set; }
    }

    private static readonly object Lock = new();

    /// <summary>Newest first.</summary>
    private static readonly List<Death> Deaths = new();

    private static int _nextId;

    /// <summary>The paramedic the last offer went to: the next death starts after them.</summary>
    private static int _lastOfferedId;

    private static System.Threading.Timer? _timer;

    /// <summary>The offer clock. Free while nothing is open: one scan of at most <see cref="WindowSize"/>.</summary>
    public static void Init() => _timer = new System.Threading.Timer(_ => Tick(), null, TickMs, TickMs);

    /// <summary>The window's deaths, newest first, as they stand now.</summary>
    public static List<Death> Newest()
    {
        lock (Lock)
            return Deaths.Select(death => death.Copy()).ToList();
    }

    /// <summary>A player has just gone down in `room`. Report it and offer it round.</summary>
    public static void OnDeath(Room room, RoomUser user)
    {
        var habbo = user?.GetClient()?.GetHabbo();
        if (room == null || user == null || user.IsBot || habbo == null)
            return;

        var root = AccountUtility.RootOf(habbo.Id);
        lock (Lock)
        {
            // Down again before an old call closed (a relog, a restart of the
            // knockout): the old one is superseded by this one.
            foreach (var old in Deaths.Where(death => death.PatientId == habbo.Id && death.IsOpen))
            {
                old.State = StateGone;
                old.ActorId = 0;
                old.ActorName = "";
            }
            Deaths.Insert(0, new Death
            {
                Id = ++_nextId,
                PatientId = habbo.Id,
                PatientRoot = root,
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

        // Offered at once rather than on the next beat, and broadcast even
        // with nobody free to take it, so it is on everyone's queue.
        Tick(forceBroadcast: true);
    }

    /// <summary>
    /// A paramedic carrying `patientId` has laid them on a hospital bed from a
    /// drop-off pad. Their call reads "Revived by" that paramedic, and whoever
    /// claimed it is free for the next one.
    /// </summary>
    public static void OnDropOff(GameClient? paramedic, int patientId)
    {
        var habbo = paramedic?.GetHabbo();
        if (habbo == null || patientId <= 0)
            return;
        var changed = false;
        lock (Lock)
        {
            var call = Deaths.FirstOrDefault(death => death.PatientId == patientId && death.IsOpen);
            if (call != null)
            {
                call.State = StateRevived;
                call.ActorId = habbo.Id;
                call.ActorName = habbo.Username;
                changed = true;
            }
        }
        if (changed)
            Tick(forceBroadcast: true);
    }

    /// <summary>The Claim button, from the paramedic the call is offered to.</summary>
    public static void Claim(GameClient session, int callId)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        if (!MedicalUtility.IsOnDutyParamedic(habbo.Id))
        {
            PushWindow(session, false);
            return;
        }

        string notice;
        Death? claimed = null;
        lock (Lock)
        {
            var call = Deaths.FirstOrDefault(death => death.Id == callId);
            if (call == null)
                notice = "That call is no longer in the queue.";
            else if (Deaths.Any(death => death.State == StateClaimed && death.ActorId == habbo.Id))
                notice = "You already have a claimed call. Drop that patient off first.";
            else if (call.State != StateOffered || call.ActorId != habbo.Id ||
                     DateTime.UtcNow > call.OfferExpiresAt.AddSeconds(ClaimGraceSeconds))
                notice = call.IsOpen ? "Too late - that call has gone to the next paramedic." : "That call is already over.";
            else
            {
                call.State = StateClaimed;
                call.ActorName = habbo.Username;
                claimed = call.Copy();
                notice = $"You claimed {call.PatientName}'s call. They are in {(call.RoomName.Length > 0 ? call.RoomName : "an unknown room")}.";
            }
        }

        if (claimed == null)
        {
            session.Send(new RpMedicalCallsComposer(true, Newest(), notice));
            return;
        }
        PlusEnvironment.Game.ClientManager.GetClientByUserId(claimed.PatientId)?
            .SendWhisper($"Paramedic {habbo.Username} is on their way.");
        Tick(forceBroadcast: true, noticeFor: habbo.Id, notice: notice);
    }

    /// <summary>
    /// Open or close one player's window: open with the deaths while they are
    /// an on-duty paramedic, closed otherwise. Either way the turn is worked
    /// out again at once - a paramedic clocking in takes a call that was
    /// waiting for somebody, and one clocking off lets theirs go round.
    /// </summary>
    public static void PushWindow(GameClient? session, bool onDuty)
    {
        if (session?.GetHabbo() == null)
            return;
        session.Send(onDuty ? new RpMedicalCallsComposer(true, Newest()) : new RpMedicalCallsComposer(false, new List<Death>()));
        Tick();
    }

    /// <summary>
    /// :calls - the window back up for an on-duty paramedic, even if they put it
    /// away with its X. The caller has checked the duty.
    /// </summary>
    public static void Reopen(GameClient session) =>
        session.Send(new RpMedicalCallsComposer(true, Newest(), reopen: true));

    private static bool AnyOpen()
    {
        lock (Lock)
            return Deaths.Any(death => death.IsOpen);
    }

    /// <summary>
    /// One beat of the queue: close the calls whose patient no longer needs a
    /// paramedic, take back offers that ran out and claims whose paramedic is
    /// off duty, and offer every waiting call to the next paramedic free.
    /// Broadcast when anything changed (or when asked to).
    /// </summary>
    private static void Tick(bool forceBroadcast = false, int noticeFor = 0, string notice = "")
    {
        try
        {
            if (!AnyOpen())
            {
                if (forceBroadcast)
                    Broadcast(OnDutyParamedics(), noticeFor, notice, new Dictionary<int, string>());
                return;
            }

            var medics = OnDutyParamedics();
            // Online as well: a paramedic who dropped connection mid-shift
            // cannot claim, and a call they claimed goes back round.
            var onDuty = medics
                .Where(medic => PlusEnvironment.Game.ClientManager.GetClientByUserId(medic.UserId)?.GetHabbo() != null)
                .Select(medic => medic.UserId)
                .ToHashSet();
            var missed = new Dictionary<int, string>();
            var changed = false;
            var now = DateTime.UtcNow;

            lock (Lock)
            {
                foreach (var call in Deaths.Where(death => death.IsOpen))
                {
                    var closed = Closed(call);
                    if (closed != StateWaiting)
                    {
                        call.State = closed;
                        call.ActorId = 0;
                        call.ActorName = "";
                        changed = true;
                        continue;
                    }

                    if (call.State == StateOffered && (now > call.OfferExpiresAt.AddSeconds(ClaimGraceSeconds) || !onDuty.Contains(call.ActorId)))
                    {
                        if (onDuty.Contains(call.ActorId))
                            missed[call.ActorId] = $"You did not claim {call.PatientName}'s call in time - it went to the next paramedic.";
                        Release(call);
                        changed = true;
                    }
                    else if (call.State == StateClaimed && !onDuty.Contains(call.ActorId))
                    {
                        Release(call);
                        changed = true;
                    }
                }

                // Oldest first: the patient who has waited longest is offered first.
                foreach (var call in Deaths.Where(death => death.State == StateWaiting).Reverse())
                {
                    var next = NextMedic(call, medics);
                    if (next == null)
                        continue;
                    // Back to the paramedic who just missed it - the only one
                    // free - is not "the next paramedic": no notice for that.
                    if (next.Value.Id == call.LastOfferedId)
                        missed.Remove(next.Value.Id);
                    call.State = StateOffered;
                    call.ActorId = next.Value.Id;
                    call.ActorName = next.Value.Name;
                    call.LastOfferedId = next.Value.Id;
                    call.OfferExpiresAt = now.AddSeconds(ClaimSeconds);
                    _lastOfferedId = next.Value.Id;
                    changed = true;
                }
            }

            if (changed || forceBroadcast)
                Broadcast(medics, noticeFor, notice, missed);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[MedicalCalls] tick failed: {exception}");
        }
    }

    /// <summary>Back into the turn, from the paramedic after the one who had it.</summary>
    private static void Release(Death call)
    {
        call.State = StateWaiting;
        call.ActorId = 0;
        call.ActorName = "";
    }

    /// <summary>
    /// Why the patient no longer needs a paramedic, as the closing state - or
    /// <see cref="StateWaiting"/> while they still do. Between rooms (being
    /// carried through a door) nothing is decided.
    /// </summary>
    private static int Closed(Death call)
    {
        var habbo = PlusEnvironment.Game.ClientManager.GetClientByUserId(call.PatientId)?.GetHabbo();
        if (habbo == null)
            return StateGone;
        var user = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (user == null)
            return StateWaiting;
        if (!user.RpKnockedOut)
            return StateRecovered;
        // On a bed with treatment under way and no drop-off recorded: the
        // hospital came for them itself (HospitalAdmission). A paramedic's
        // drop-off has already closed the call as Revived before this runs.
        if (user.MedicalBedId != 0)
            return StateAdmitted;
        return StateWaiting;
    }

    /// <summary>
    /// The next paramedic in turn who is free: on duty, online, not holding a
    /// claim or an unanswered offer, and not the patient on another character.
    /// The turn goes up by user id from whoever had this call last (or, for a
    /// new call, whoever was offered one last) and wraps round - back to the
    /// same paramedic when they are the only one free.
    /// </summary>
    private static (int Id, string Name)? NextMedic(Death call, List<Medic> medics)
    {
        var busy = Deaths
            .Where(death => death.State is StateOffered or StateClaimed)
            .Select(death => death.ActorId)
            .ToHashSet();
        var free = new List<(int Id, string Name)>();
        foreach (var medic in medics.OrderBy(medic => medic.UserId))
        {
            if (busy.Contains(medic.UserId) || medic.UserId == call.PatientId || medic.Root == call.PatientRoot)
                continue;
            var habbo = PlusEnvironment.Game.ClientManager.GetClientByUserId(medic.UserId)?.GetHabbo();
            if (habbo == null)
                continue;
            free.Add((medic.UserId, habbo.Username));
        }
        if (free.Count == 0)
            return null;
        var after = call.LastOfferedId != 0 ? call.LastOfferedId : _lastOfferedId;
        foreach (var medic in free)
            if (medic.Id > after)
                return medic;
        return free[0];
    }

    /// <summary>
    /// The queue to every on-duty paramedic and above; `noticeFor` alone is
    /// also told `notice`, and each paramedic in `missed` is told theirs.
    /// </summary>
    private static void Broadcast(List<Medic> medics, int noticeFor, string notice, Dictionary<int, string> missed)
    {
        var deaths = Newest();
        foreach (var medic in medics)
        {
            var told = medic.UserId == noticeFor ? notice : missed.GetValueOrDefault(medic.UserId, "");
            PlusEnvironment.Game.ClientManager.GetClientByUserId(medic.UserId)?.Send(new RpMedicalCallsComposer(true, deaths, told));
        }
    }

    /// <summary>
    /// The hospital's on-duty staff at a rank cleared for emergency work - the
    /// same three halves as <see cref="MedicalUtility.IsOnDutyParamedic"/>, for
    /// everybody at once - with the account each is on.
    /// </summary>
    private static List<Medic> OnDutyParamedics()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<Medic>(
                "SELECT DISTINCT e.`user_id` AS UserId, CAST(COALESCE(NULLIF(u.`parent_id`, 0), u.`id`) AS SIGNED) AS Root " +
                "FROM `rp_corporation_employees` e " +
                "JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
                "JOIN `rp_corporation_ranks` r ON r.`id` = e.`rank_id` " +
                "JOIN `users` u ON u.`id` = e.`user_id` " +
                "WHERE e.`on_duty` = 1 AND c.`service_type` = 'medical' AND r.`emergency_eligible` = 1")
            .ToList();
    }
}
