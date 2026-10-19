using Dapper;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: the hospital's emergency queue, Paramedic Dispatch - 911
/// Dispatch's counterpart for paramedics (EmergencyCalls).
///
/// Nobody calls it: every time a player goes down (0 health, from
/// RoomUserManager.ApplyRpKnockout) the queue gets an entry - who went down and
/// the room they went down in - dispatched to on-duty paramedics and above
/// (the ranks <see cref="MedicalUtility.IsOnDutyParamedic"/> clears for
/// emergency work).
///
/// A paramedic sees ONLY their live call (2026-10-18, no history): the one
/// offered to them, or the one they accepted until it ends - never another
/// paramedic's, and nothing once it is over (<see cref="IsTheirs"/>). A call
/// they decline or let run out, or one that ends, simply leaves their window,
/// and the window closes with it. It is sent on clock-in and taken away on
/// clock-out (PushWindow, from PoliceUtility.PushPardonRights, which runs at
/// exactly those moments), but it only comes up on screen when a call is
/// offered to them; its X puts it away until the next one is. There is no
/// :calls for paramedics (Twist, 2026-10-18) - that is the police's 911
/// Dispatch only.
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
/// Accepting gives the paramedic <see cref="ReviveSeconds"/> to get the
/// patient: after that the hospital collects them itself (HospitalAdmission),
/// whatever its usual clock says. <see cref="ReminderSeconds"/> before then,
/// a paramedic who has not picked the patient up yet is whispered a reminder.
///
/// NO PARAMEDIC ON DUTY (Twist, 2026-10-19). A knockout with nobody on duty to
/// send - not counting the patient's own characters - is not left to the
/// usual hospital clock: the hospital comes for a player in
/// <see cref="NoMedicSeconds"/>, and for VIP and staff straight away, each told
/// so instead of "Your call has been sent!". The call is still queued, so a
/// paramedic who clocks in meanwhile is offered it.
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
    /// <summary>How many of the newest deaths the queue keeps.</summary>
    public const int WindowSize = 20;

    /// <summary>How long one paramedic has to claim a call before it moves on.</summary>
    public const int ClaimSeconds = 30;

    /// <summary>A claim pressed on the last beat still counts - the trip from the client takes time.</summary>
    private const double ClaimGraceSeconds = 1.0;

    /// <summary>From Accept, how long the paramedic has before the hospital collects the patient itself.</summary>
    public const int ReviveSeconds = 120;

    /// <summary>How long before that the paramedic is reminded, if they have not picked the patient up.</summary>
    private const int ReminderSeconds = 30;

    /// <summary>Coins a paramedic is tipped for laying a patient on a hospital bed.</summary>
    public const int DropOffTip = 3;

    /// <summary>No paramedic on duty: how long until the hospital collects the patient.</summary>
    private const int NoMedicSeconds = 60;

    /// <summary>... and for VIP and staff - at once, but after their whisper (<see cref="AfterBubbleMs"/>).</summary>
    private const int NoMedicVipSeconds = 2;

    private const string NoMedicWhisper = "You'll be automatically transported to the hospital in 1 minute.";

    private const string NoMedicVipWhisper = "There are no paramedics on duty, you will be automatically transported to the hospital.";

    private const string ReminderWhisper = "You have 30 seconds to retrieve the patient!";

    private const string LapsedWhisper = "You took too long to accept the call. It will be assigned to another paramedic.";

    private const string CollectedWhisper = "You took too long to retrieve your patient. They have been auto-revived at the hospital.";

    // The accepted call ending without its paramedic's drop-off: the card
    // leaves their window, so they are told why (Twist, 2026-10-18).
    private const string RecoveredWhisper = "Your patient has been revived.";

    private const string GoneWhisper = "Your patient has logged out.";

    private const int TickMs = 1000;

    /// <summary>
    /// How long <see cref="WhisperAfterBubble"/> waits - longer than a
    /// knockout's "has ended their shift" (ShiftManager), which comes first.
    /// </summary>
    private const int AfterBubbleMs = 500;

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
        /// <summary>Paramedics who were offered it and let it lapse or declined it: never offered it again.</summary>
        public HashSet<int> MissedBy { get; init; } = new();
        /// <summary>While claimed: when the hospital collects the patient if the paramedic has not.</summary>
        public DateTime ReviveBy { get; set; }
        /// <summary>The claimer has had their <see cref="ReminderSeconds"/> whisper.</summary>
        public bool Reminded { get; set; }
        /// <summary>
        /// Another paramedic had it first - offered and let go, or claimed and
        /// dropped - so whoever accepts it now is taking it over.
        /// </summary>
        public bool PassedOn { get; set; }

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

    /// <summary>Every death, newest first, as they stand now.</summary>
    private static List<Death> Snapshot()
    {
        lock (Lock)
            return Deaths.Select(death => death.Copy()).ToList();
    }

    /// <summary>One paramedic's window: their live call, if they have one.</summary>
    public static List<Death> Newest(int userId) =>
        Snapshot().Where(death => IsTheirs(death, userId)).ToList();

    /// <summary>
    /// Offered to them right now, or accepted by them and not over yet. Nothing
    /// else: a call they declined or let run out, one that has ended - dropped
    /// off, back on their feet, collected by the hospital, logged out - and
    /// anybody else's are not kept (no history, Twist 2026-10-18).
    /// </summary>
    private static bool IsTheirs(Death death, int userId) =>
        userId > 0 && death.ActorId == userId && death.State is StateOffered or StateClaimed;

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

        // Nobody to send: the hospital comes sooner. Not for a prisoner - the
        // hospital waits for their release (HospitalAdmission), so they would
        // be told of a trip that is not coming.
        if (!AnyMedicFor(habbo.Id, root) && !JailState.IsJailed(habbo.Id))
        {
            var vip = habbo.IsVip || habbo.IsStaff;
            HospitalAdmission.CollectBy(habbo.Id, DateTime.UtcNow.AddSeconds(vip ? NoMedicVipSeconds : NoMedicSeconds), told: true);
            WhisperAfterBubble(user.GetClient(), vip ? NoMedicVipWhisper : NoMedicWhisper);
        }
        else
            WhisperAfterBubble(user.GetClient(), "Your call has been sent! A paramedic will be assigned shortly.");

        // Offered at once rather than on the next beat, and broadcast even
        // with nobody free to take it, so it is on everyone's queue.
        Tick(forceBroadcast: true);
    }

    /// <summary>
    /// A paramedic carrying `patientId` has laid them on a hospital bed from a
    /// drop-off pad. Their call closes as revived by that paramedic, its card
    /// leaves the window, and whoever claimed it is free for the next one.
    /// </summary>
    public static void OnDropOff(GameClient? paramedic, int patientId)
    {
        var habbo = paramedic?.GetHabbo();
        if (habbo == null || patientId <= 0)
            return;
        var changed = false;
        // Another paramedic had accepted this call: their card goes with it,
        // so they are told who dropped their patient off.
        var claimerId = 0;
        lock (Lock)
        {
            var call = Deaths.FirstOrDefault(death => death.PatientId == patientId && death.IsOpen);
            if (call != null)
            {
                if (call.State == StateClaimed && call.ActorId != habbo.Id)
                    claimerId = call.ActorId;
                call.State = StateRevived;
                call.ActorId = habbo.Id;
                call.ActorName = habbo.Username;
                changed = true;
            }
        }
        if (!changed)
            return;
        // The call is marked above, at once and in memory only: the patient is
        // already on the bed, and the 1-second tick would otherwise close it as
        // Admitted (Closed). The rest - the tip, and the broadcast that reads
        // the on-duty paramedics - writes to and reads the database, and the
        // drop-off pad fires on the movement thread (a tile effect, under the
        // room's _cycleLock), so it runs on RoomTickWriter's thread instead.
        RoomTickWriter.QueueOrdered(() =>
        {
            try
            {
                if (claimerId != 0)
                    PlusEnvironment.Game.ClientManager.GetClientByUserId(claimerId)?
                        .SendWhisper($"Your patient has been dropped off by {habbo.Username}.");
                // Tipped once per knockout - the call closes here, so a second
                // drop-off of the same patient finds nothing open - and never for
                // a patient on the paramedic's own account.
                if (!AccountUtility.SameAccount(habbo.Id, patientId))
                {
                    CityPlayers.AdjustCreditsFor(habbo.Id, DropOffTip, "Hospital drop-off tip");
                    WhisperAfterBubble(paramedic, $"You just earned a {DropOffTip}c tip for escorting a patient to the hospital!");
                }
            }
            finally
            {
                // The windows show the drop-off even if the tip could not be paid.
                Tick(forceBroadcast: true);
            }
        });
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
                call.ReviveBy = DateTime.UtcNow.AddSeconds(ReviveSeconds);
                call.Reminded = false;
                claimed = call.Copy();
                // pixelrp: no "you claimed" notice - pressing Claim is confirmation enough (Twist).
                notice = "";
            }
        }

        if (claimed == null)
        {
            session.Send(new RpMedicalCallsComposer(true, Newest(habbo.Id), notice));
            return;
        }
        HospitalAdmission.CollectBy(claimed.PatientId, claimed.ReviveBy);
        PlusEnvironment.Game.ClientManager.GetClientByUserId(claimed.PatientId)?
            .SendWhisper($"Paramedic {habbo.Username} is on their way.");
        // Said out loud either way: a call another paramedic had first is
        // taken over, with a whisper of where to go; a fresh one is accepted.
        if (claimed.PassedOn)
        {
            session.SendWhisper($"You have taken over {claimed.PatientName}'s call. Head to {Location(claimed)} to retrieve them!");
            ActionBubble(habbo.Id, $"*takes over {claimed.PatientName}'s call*");
        }
        else
            ActionBubble(habbo.Id, $"*accepts {claimed.PatientName}'s call*");
        Tick(forceBroadcast: true, noticeFor: habbo.Id, notice: notice);
    }

    /// <summary>
    /// The Decline button, from the paramedic the call is offered to: the same
    /// as letting the offer run out, only now - the blue bubble over them, the
    /// card gone from their window, never offered to them again, and on to the
    /// next paramedic at once.
    /// </summary>
    public static void Decline(GameClient session, int callId)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        if (!MedicalUtility.IsOnDutyParamedic(habbo.Id))
        {
            PushWindow(session, false);
            return;
        }

        var declined = false;
        lock (Lock)
        {
            var call = Deaths.FirstOrDefault(death => death.Id == callId);
            if (call != null && call.State == StateOffered && call.ActorId == habbo.Id)
            {
                call.MissedBy.Add(habbo.Id);
                Release(call);
                declined = true;
            }
        }

        // Already claimed, moved on or over: just their window as it stands.
        if (!declined)
        {
            session.Send(new RpMedicalCallsComposer(true, Newest(habbo.Id)));
            return;
        }
        AnnounceMiss(habbo.Id);
        Tick(forceBroadcast: true);
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
        session.Send(onDuty ? new RpMedicalCallsComposer(true, Newest(session.GetHabbo().Id)) : new RpMedicalCallsComposer(false, new List<Death>()));
        Tick();
    }

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
            // Sent once the lock is let go: (paramedic, whisper).
            var whispers = new List<(int UserId, string Text)>();
            var changed = false;
            var now = DateTime.UtcNow;

            lock (Lock)
            {
                foreach (var call in Deaths.Where(death => death.IsOpen))
                {
                    var closed = Closed(call);
                    if (closed != StateWaiting)
                    {
                        // An accepted call ending without its paramedic's
                        // drop-off: their card goes, so they hear why. The
                        // hospital coming first is only ever its ReviveSeconds
                        // deadline (HospitalAdmission.CollectBy) - the claimer
                        // never got to them.
                        if (call.State == StateClaimed)
                        {
                            var why = closed switch
                            {
                                StateAdmitted => CollectedWhisper,
                                StateRecovered => RecoveredWhisper,
                                StateGone => GoneWhisper,
                                _ => null
                            };
                            if (why != null)
                                whispers.Add((call.ActorId, why));
                        }
                        call.State = closed;
                        call.ActorId = 0;
                        call.ActorName = "";
                        changed = true;
                        continue;
                    }

                    if (call.State == StateOffered && (now > call.OfferExpiresAt.AddSeconds(ClaimGraceSeconds) || !onDuty.Contains(call.ActorId)))
                    {
                        // A paramedic who let their offer lapse (not one who just
                        // clocked off): a whisper and a blue action bubble over
                        // them, and they never get THIS call again, even with
                        // nobody else free.
                        if (onDuty.Contains(call.ActorId))
                        {
                            whispers.Add((call.ActorId, LapsedWhisper));
                            AnnounceMiss(call.ActorId);
                            call.MissedBy.Add(call.ActorId);
                        }
                        Release(call);
                        changed = true;
                    }
                    else if (call.State == StateClaimed && !onDuty.Contains(call.ActorId))
                    {
                        Release(call);
                        changed = true;
                    }
                    else if (call.State == StateClaimed && !call.Reminded && now >= call.ReviveBy.AddSeconds(-ReminderSeconds))
                    {
                        // Once per claim, and not to a paramedic already carrying
                        // the patient - they have retrieved them.
                        call.Reminded = true;
                        if (PoliceState.CaptorOf(call.PatientId) != call.ActorId)
                            whispers.Add((call.ActorId, ReminderWhisper));
                    }
                }

                // Oldest first: the patient who has waited longest is offered first.
                foreach (var call in Deaths.Where(death => death.State == StateWaiting).Reverse())
                {
                    var next = NextMedic(call, medics);
                    if (next == null)
                        continue;
                    call.State = StateOffered;
                    call.ActorId = next.Value.Id;
                    call.ActorName = next.Value.Name;
                    call.LastOfferedId = next.Value.Id;
                    call.OfferExpiresAt = now.AddSeconds(ClaimSeconds);
                    whispers.Add((next.Value.Id, $"You have been assigned a call at {Location(call)}. You have {ClaimSeconds} seconds to accept."));
                    _lastOfferedId = next.Value.Id;
                    changed = true;
                }
            }

            foreach (var (userId, text) in whispers)
                PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.SendWhisper(text);

            if (changed || forceBroadcast)
                Broadcast(medics, noticeFor, notice, missed);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[MedicalCalls] tick failed: {exception}");
        }
    }

    /// <summary>
    /// A whisper that has to land AFTER the action bubble of whatever caused
    /// it, so it waits a beat. Its callers run in the middle of things whose
    /// bubble comes once they return: :kill's lightning and :sethealth speak
    /// after the knockout (OnDeath), and the drop-off pad's "moves X onto the
    /// medical bed" after the drop (OnDropOff).
    /// </summary>
    private static void WhisperAfterBubble(GameClient? client, string text)
    {
        if (client == null)
            return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(AfterBubbleMs);
            try
            {
                client.SendWhisper(text);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[MedicalCalls] whisper failed: {exception}");
            }
        });
    }

    /// <summary>Where the patient went down, for a whisper: the room's name, or "the patient's location" without one.</summary>
    private static string Location(Death call) =>
        string.IsNullOrEmpty(call.RoomName) ? "the patient's location" : call.RoomName;

    /// <summary>A blue action bubble over a paramedic who let their offer lapse or declined it.</summary>
    private static void AnnounceMiss(int medicId) => ActionBubble(medicId, "*declines this call*");

    /// <summary>A blue action bubble over a paramedic, in whatever room they are in.</summary>
    private static void ActionBubble(int medicId, string text)
    {
        var room = PlusEnvironment.Game.ClientManager.GetClientByUserId(medicId)?.GetHabbo()?.CurrentRoom;
        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(medicId);
        if (user != null)
            room!.SendPacket(new ChatComposer(user.VirtualId, text, 0, 4));
    }

    /// <summary>Back into the turn, from the paramedic after the one who had it.</summary>
    private static void Release(Death call)
    {
        call.State = StateWaiting;
        call.ActorId = 0;
        call.ActorName = "";
        call.PassedOn = true;
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
            if (busy.Contains(medic.UserId) || call.MissedBy.Contains(medic.UserId) || medic.UserId == call.PatientId || medic.Root == call.PatientRoot)
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
    /// Is any paramedic on duty who could be sent to this patient: online, and
    /// not the patient on any of their characters.
    /// </summary>
    private static bool AnyMedicFor(int patientId, int patientRoot) =>
        OnDutyParamedics().Any(medic => medic.UserId != patientId && medic.Root != patientRoot &&
            PlusEnvironment.Game.ClientManager.GetClientByUserId(medic.UserId)?.GetHabbo() != null);

    /// <summary>
    /// Every on-duty paramedic and above their own queue - so a call that
    /// moved on leaves the window of the one who missed it; `noticeFor` alone
    /// is also told `notice`, and each paramedic in `missed` is told theirs.
    /// </summary>
    private static void Broadcast(List<Medic> medics, int noticeFor, string notice, Dictionary<int, string> missed)
    {
        foreach (var medic in medics)
        {
            var told = medic.UserId == noticeFor ? notice : missed.GetValueOrDefault(medic.UserId, "");
            var theirs = Newest(medic.UserId);
            PlusEnvironment.Game.ClientManager.GetClientByUserId(medic.UserId)?.Send(new RpMedicalCallsComposer(true, theirs, told));
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
