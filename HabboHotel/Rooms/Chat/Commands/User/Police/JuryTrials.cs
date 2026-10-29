using System.Collections.Concurrent;
using System.Drawing;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp jury trials (Twist, 2026-10-19/20).
///
/// A prisoner serving 15 minutes or more, with 5 or more still to run, may ask
/// once per sentence for a trial (JailState.RequestTrial - the Request a Trial
/// card, or :trial) while at least <see cref="MinJurors"/> players could serve.
/// Requests queue, first come first served: there is one Courthouse
/// (<see cref="CourthouseRoomId"/>), so one trial at a time.
///
/// SUMMONS. Up to <see cref="MaxJurors"/> players are picked at random from
/// everybody who could serve - online, not serving time, not knocked out, not
/// cuffed or being escorted; police, the arresting officer, the prisoner's gang
/// and sleeping players included; never a system account. Each is whispered to
/// get to the Courthouse within two minutes, and reminded with 30 seconds
/// left. The prisoner's clock keeps running meanwhile, in the jail.
///
/// COURT starts the moment the two minutes end, with whichever picked players
/// are in the Courthouse then and not in cuffs - one is enough; nobody, and
/// the trial is called off, as it is when every juror has logged out and none
/// is back within <see cref="EmptyGraceSeconds"/>, court standing still
/// meanwhile. Anybody else can walk in and watch; a latecomer is not a juror. The
/// jurors are put on the jury seats - every <see cref="JurySeatItemName"/> in
/// the Courthouse (Twist, 2026-10-29) - and can't walk off them until court is
/// out (<see cref="HeldInJuryBox"/>). Nobody else may walk onto one, trial or
/// no trial (<see cref="IsJurySeat"/>). The jury box is sealed off, so once
/// court is out the jury is taken to the lobby seats (<see cref="ToLobby"/>). The
/// prisoner is brought over (their clock stopping as they arrive) and the
/// judge - whichever bot is in the Courthouse - opens the court and reads the
/// charges, gives the defendant 15 seconds to speak, and puts it to the vote.
/// The jurors and the prisoner cannot leave until it is over, short of logging
/// out. A juror who logs back in while court is still in session is back on the
/// jury, on their own seat (Twist, 2026-10-29).
///
/// THE VOTE is the jurors' vote card: Guilty or Not Guilty, ten seconds. The
/// verdict comes when every juror in court has voted or the time is up: more
/// Not Guilty than Guilty is not guilty; anything else - a tie, no votes - is
/// guilty. The judge reads it out, every juror still in court is paid
/// <see cref="JuryPay"/> coins, and the prisoner goes back to the jail - to
/// serve the rest, or to be let go there ("You are free to go.").
///
/// CALLED OFF when the prisoner is let go (time served, staff) before the
/// verdict, is not online when court starts, logs out or never arrives once it
/// has, or nobody turns up - the jurors are told, nobody is paid, and the
/// request is spent. The one paid cancellation is the prisoner logging out once
/// court has started: the judge says so, and every juror in court is paid as at
/// a verdict (Twist, 2026-10-20). Staff can't release a prisoner once court has
/// started (<see cref="InCourtNow"/>). A restart is the exception: the trial dies with the
/// process, so an unfinished request is given back (<see cref="Init"/>).
///
/// Held in memory, like Paramedic Dispatch; the sentence's own record keeps
/// the request and how it ended.
/// </summary>
public static class JuryTrials
{
    /// <summary>Fewer players than this online who could serve, and no trial can be asked for.</summary>
    public const int MinJurors = 3;

    /// <summary>The most players summoned to one trial.</summary>
    public const int MaxJurors = 12;

    /// <summary>The Courthouse (Twist, 2026-10-19).</summary>
    public const uint CourthouseRoomId = 26;

    /// <summary>The jury's seats: every Chair 5 in the Courthouse (Twist, 2026-10-29).</summary>
    public const string JurySeatItemName = "habbox_haaziq_chaise5";

    /// <summary>The lobby seats the jury is taken to once court is out: every Arm Chair and Sofa 2 in the Courthouse (Twist, 2026-10-29).</summary>
    private static readonly string[] LobbySeatItemNames = { "belmondblaira_armchair", "belmondfolasade_loveseat" };

    /// <summary>What each juror still in court at the verdict (or when the prisoner logs out of it) is paid.</summary>
    public const int JuryPay = 25;

    public const int VoteGuilty = 1;
    public const int VoteNotGuilty = 2;

    /// <summary>`rp_jail`.`trial_result`.</summary>
    public const int ResultGuilty = 1;
    public const int ResultNotGuilty = 2;
    public const int ResultCancelled = 3;

    private const int SummonsSeconds = 120;
    private const int ReminderSeconds = 30;
    /// <summary>How long the prisoner has to arrive once court starts before it is called off.</summary>
    private const int ArriveSeconds = 20;
    /// <summary>
    /// How long court waits, standing still, for a juror to log back in once
    /// every juror has logged out, before it is called off (Twist, 2026-10-29).
    /// </summary>
    private const int EmptyGraceSeconds = 30;
    /// <summary>Between the judge's opening lines, so the charges do not come all at once.</summary>
    private const int LineSeconds = 2;
    private const int PleaSeconds = 15;
    private const int VoteSeconds = 10;
    /// <summary>Between the verdict and the prisoner being taken back, so they hear it.</summary>
    private const int VerdictPauseSeconds = 3;
    private const int TickMs = 1000;
    /// <summary>How often the jury seats are checked for anybody left on one (<see cref="SweepJurySeats"/>).</summary>
    private const int SweepSeconds = 5;
    /// <summary>How long the list of system accounts is trusted before it is read again.</summary>
    private const int SystemAccountsMinutes = 10;

    private const string InviteWhisper = "You have been invited to attend jury duty. You have 2 minutes to head to the Courthouse.";
    private const string ReminderWhisper = "You have 30 seconds to attend jury duty.";
    private const string NoJurorsWhisper = "Your trial has been cancelled because there are no jurors in attendance.";
    private const string CancelledWhisper = "The trial has been cancelled.";
    private const string DefendantLeftLine = "The defendant has left the court. This trial has been cancelled.";

    private enum Phase
    {
        Summons,
        Arriving,
        Opening,
        Plea,
        Voting,
        Verdict
    }

    private sealed class Trial
    {
        public int PrisonerId { get; init; }
        public string Name { get; set; } = "";
        public string Look { get; set; } = "";
        public string Gender { get; set; } = "M";
        public Phase Phase { get; set; }
        public DateTime PhaseEndsUtc { get; set; }
        public bool Reminded { get; set; }
        /// <summary>Everybody summoned.</summary>
        public HashSet<int> Picked { get; } = new();
        /// <summary>The summoned who were in court when it started: the jury.</summary>
        public HashSet<int> Jurors { get; set; } = new();
        /// <summary>The jury as court started, logged out since or not: who may come back to it.</summary>
        public HashSet<int> Sworn { get; set; } = new();
        /// <summary>Each juror's jury seat (item id), for one who logs back in while court is in session.</summary>
        public ConcurrentDictionary<int, uint> SeatOf { get; } = new();
        public Dictionary<int, int> Votes { get; } = new();
        public Queue<string> Lines { get; } = new();
        public DateTime NextLineUtc { get; set; }
        /// <summary>Since when every juror has been logged out - court stands still until one is back (<see cref="EmptyGraceSeconds"/>).</summary>
        public DateTime? EmptySinceUtc { get; set; }
    }

    private static readonly object Lock = new();

    /// <summary>Prisoners waiting their turn, first come first served.</summary>
    private static readonly List<int> Waiting = new();

    private static Trial? _active;

    /// <summary>
    /// The jury while court is in session, kept to the jury box. Apart from
    /// <see cref="_active"/> so a click to walk (MoveAvatarEvent) reads it
    /// without waiting on <see cref="Lock"/>.
    /// </summary>
    private static readonly ConcurrentDictionary<int, byte> JuryBox = new();

    private static System.Threading.Timer? _timer;
    private static int _ticking;
    private static DateTime _nextSweepUtc = DateTime.MinValue;

    private static volatile HashSet<int> _systemAccounts = new();
    private static DateTime _systemAccountsReadUtc = DateTime.MinValue;

    /// <summary>
    /// At start-up: a request a restart cut short - asked, not concluded, and
    /// the sentence still running - is given back (Twist, 2026-10-20), and the
    /// court's clock starts.
    /// </summary>
    public static void Init()
    {
        try
        {
            using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
            dbClient.RunQuery("UPDATE `rp_jail` SET `trial_requested_at` = 0 WHERE `released_at` = 0 AND `trial_requested_at` > 0 AND `trial_result` = 0");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[JuryTrials] giving back unfinished requests failed: {exception}");
        }
        _timer = new System.Threading.Timer(_ => Tick(), null, TickMs, TickMs);
    }

    // ---- who could serve ------------------------------------------------------

    /// <summary>How many players could be called to a jury right now, leaving out <paramref name="exceptId"/>.</summary>
    public static int JurorsAvailable(int exceptId) => EligibleJurors(exceptId).Count;

    private static List<int> EligibleJurors(int exceptId)
    {
        var system = SystemAccounts();
        var ids = new List<int>();
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
        {
            var habbo = client?.GetHabbo();
            if (habbo == null || habbo.Id == exceptId || system.Contains(habbo.Id) || JailState.IsJailed(habbo.Id) ||
                PoliceState.IsCuffed(habbo.Id) || PoliceState.IsBeingEscorted(habbo.Id))
                continue;
            if (habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)?.RpKnockedOut == true)
                continue;
            ids.Add(habbo.Id);
        }
        return ids;
    }

    /// <summary>
    /// The accounts that are not players (`users`.`rp_system_account`). A
    /// handful at most and they hardly ever change, so they are read now and
    /// then rather than for every player counted.
    /// </summary>
    private static HashSet<int> SystemAccounts()
    {
        if ((DateTime.UtcNow - _systemAccountsReadUtc).TotalMinutes < SystemAccountsMinutes)
            return _systemAccounts;
        var ids = new HashSet<int>();
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT `id` FROM `users` WHERE `rp_system_account` = 1");
            var table = dbClient.GetTable();
            if (table != null)
            {
                foreach (System.Data.DataRow row in table.Rows)
                    ids.Add(Convert.ToInt32(row["id"]));
            }
        }
        _systemAccounts = ids;
        _systemAccountsReadUtc = DateTime.UtcNow;
        return ids;
    }

    // ---- what the rest of the hotel tells the court -------------------------

    /// <summary>A prisoner's request was recorded (JailState.RequestTrial): into the queue.</summary>
    public static void Enqueue(int prisonerId)
    {
        lock (Lock)
        {
            if (!Waiting.Contains(prisonerId) && _active?.PrisonerId != prisonerId)
                Waiting.Add(prisonerId);
        }
    }

    /// <summary>
    /// True from the moment court starts until the prisoner is taken back to
    /// the jail - staff can't release them in that time (Twist, 2026-10-20).
    /// </summary>
    public static bool InCourtNow(int prisonerId)
    {
        lock (Lock)
            return _active is { } trial && trial.PrisonerId == prisonerId && trial.Phase >= Phase.Arriving;
    }

    /// <summary>
    /// True from the moment the prisoner arrives in the Courthouse for their
    /// trial until it is over - their trial card is hidden then (Twist, 2026-10-20).
    /// </summary>
    public static bool ArrivedInCourt(int prisonerId)
    {
        lock (Lock)
            return _active is { } trial && trial.PrisonerId == prisonerId && trial.Phase >= Phase.Opening;
    }

    /// <summary>A prisoner was let go (JailState.Release, a staff release): any trial of theirs still to come is off.</summary>
    public static void OnReleased(int habboId)
    {
        var after = new List<Action>();
        lock (Lock)
        {
            Waiting.Remove(habboId);
            if (_active is { } trial && trial.PrisonerId == habboId && trial.Phase != Phase.Verdict)
                Cancel(trial, trial.Phase == Phase.Summons ? trial.Picked : trial.Jurors, null, false, after);
        }
        Run(after);
    }

    /// <summary>
    /// Somebody logged out. The prisoner, once court has started, calls it off -
    /// the judge says so and the jurors in court are still paid for coming; in
    /// the summons they may still come back in time. A juror in court gives up
    /// their vote and their pay - unless they are back before the verdict
    /// (<see cref="OnRoomEntered"/>); the last one gone, and court stands still
    /// for <see cref="EmptyGraceSeconds"/> - nobody back, and there is no trial.
    /// </summary>
    public static void OnLogout(int habboId)
    {
        var after = new List<Action>();
        lock (Lock)
        {
            if (_active is { } trial && trial.Phase is >= Phase.Arriving and < Phase.Verdict)
            {
                if (trial.PrisonerId == habboId)
                {
                    Say(DefendantLeftLine, after);
                    Cancel(trial, trial.Jurors, null, false, after, payJurors: true);
                }
                else if (trial.Jurors.Remove(habboId))
                {
                    JuryBox.TryRemove(habboId, out _);
                    trial.Votes.Remove(habboId);
                    // Every juror logged out: court stands still in case one is
                    // back (Twist, 2026-10-29); nobody, and it is called off
                    // (Twist, 2026-10-20) - Step.
                    if (!trial.Jurors.Any(InCourt))
                        trial.EmptySinceUtc = DateTime.UtcNow;
                }
            }
        }
        Run(after);
    }

    /// <summary>
    /// Somebody came into the Courthouse. The prisoner, for their trial: court
    /// opens. A juror back while court is in session: their seat. Anybody else
    /// landing on a jury seat: the lobby.
    /// </summary>
    public static void OnRoomEntered(Room room, RoomUser user)
    {
        if (room == null || user == null || user.IsBot || room.RoomId != CourthouseRoomId)
            return;
        var after = new List<Action>();
        var habboId = user.UserId;
        lock (Lock)
        {
            if (_active is { Phase: Phase.Arriving } trial && trial.PrisonerId == habboId)
                BeginCourt(trial, DateTime.UtcNow, after);
            // A juror who logged out of court, back while it is still in
            // session: back on the jury - their vote while it is open, and their
            // pay - and back on their seat (Twist, 2026-10-29).
            else if (_active is { Phase: >= Phase.Arriving and < Phase.Verdict } court && court.Sworn.Contains(habboId))
            {
                if (court.Jurors.Add(habboId))
                {
                    JuryBox[habboId] = 0;
                    CarryOn(court, DateTime.UtcNow);
                    if (court.Phase == Phase.Voting)
                        SendCards(court, DateTime.UtcNow, after);
                }
                after.Add(() => ReturnToSeat(court, habboId));
            }
        }
        // Anybody else on a jury seat - a juror back once court is out, or
        // after a restart - and the box is sealed: out to the lobby.
        if (!HeldInJuryBox(habboId) && IsJurySeat(room, user.X, user.Y))
            after.Add(() => ToLobby(new List<int> { habboId }));
        Run(after);
    }

    /// <summary>
    /// Is this player held in the Courthouse - a juror or the prisoner while
    /// court is in session - so may not go to <paramref name="targetRoomId"/>?
    /// (Habbo.PrepareRoom)
    /// </summary>
    public static bool HeldInCourt(int habboId, uint targetRoomId)
    {
        if (targetRoomId == CourthouseRoomId)
            return false;
        lock (Lock)
        {
            if (_active is not { } trial || trial.Phase < Phase.Arriving || !InCourt(habboId))
                return false;
            return trial.Jurors.Contains(habboId) || (trial.PrisonerId == habboId && trial.Phase > Phase.Arriving);
        }
    }

    /// <summary>
    /// Is this player on the jury while court is in session, so may not walk
    /// off the jury seats? (MoveAvatarEvent)
    /// </summary>
    public static bool HeldInJuryBox(int habboId) => JuryBox.ContainsKey(habboId);

    /// <summary>
    /// Is this tile a jury seat, which nobody may walk onto? Only the jury sits
    /// there, put on them as court starts - watchers are not allowed in the
    /// jury seating area (Twist, 2026-10-29). (MoveAvatarEvent)
    /// </summary>
    public static bool IsJurySeat(Room room, int x, int y) =>
        room != null && room.RoomId == CourthouseRoomId &&
        room.GetGameMap()?.GetAllRoomItemForSquare(x, y).Any(item => item?.Definition?.ItemName == JurySeatItemName) == true;

    /// <summary>A juror's Guilty or Not Guilty (RpJuryVoteEvent). One vote each, while the vote is open, in court.</summary>
    public static void Vote(GameClient session, int vote)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || (vote != VoteGuilty && vote != VoteNotGuilty))
            return;
        var after = new List<Action>();
        lock (Lock)
        {
            if (_active is { Phase: Phase.Voting } trial && trial.Jurors.Contains(habbo.Id) && InCourt(habbo.Id) && !trial.Votes.ContainsKey(habbo.Id))
            {
                trial.Votes[habbo.Id] = vote;
                SendCards(trial, DateTime.UtcNow, after);
            }
        }
        Run(after);
    }

    // ---- the court's clock ----------------------------------------------------

    private static void Tick()
    {
        if (Interlocked.Exchange(ref _ticking, 1) == 1)
            return;
        try
        {
            var now = DateTime.UtcNow;
            var after = new List<Action>();
            lock (Lock)
                Step(now, after);
            Run(after);
            if (now >= _nextSweepUtc)
            {
                _nextSweepUtc = now.AddSeconds(SweepSeconds);
                SweepJurySeats();
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[JuryTrials] tick failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _ticking, 0);
        }
    }

    /// <summary>
    /// One beat. Decides under the lock; everything it sends, writes or moves
    /// goes into <paramref name="after"/>, run once the lock is let go.
    /// </summary>
    private static void Step(DateTime now, List<Action> after)
    {
        if (_active == null)
        {
            while (_active == null && Waiting.Count > 0)
            {
                var prisonerId = Waiting[0];
                Waiting.RemoveAt(0);
                // Let go while waiting (a staff release offline): nothing to try.
                if (!JailState.IsJailed(prisonerId) && JailState.SecondsLeftAnywhere(prisonerId) <= 0)
                    continue;
                StartSummons(prisonerId, now, after);
            }
            return;
        }

        var trial = _active;
        // No juror in court: it stands still - no lines, no clocks - until one
        // is back or the wait is up.
        if (trial.EmptySinceUtc is { } emptySince)
        {
            if (trial.Jurors.Any(InCourt))
                CarryOn(trial, now);
            else
            {
                if (now >= emptySince.AddSeconds(EmptyGraceSeconds))
                    Cancel(trial, Array.Empty<int>(), NoJurorsWhisper, true, after);
                return;
            }
        }
        switch (trial.Phase)
        {
            case Phase.Summons:
                StepSummons(trial, now, after);
                break;
            case Phase.Arriving:
                // Brought over, and never got there.
                if (now >= trial.PhaseEndsUtc)
                    Cancel(trial, trial.Jurors, null, true, after);
                break;
            case Phase.Opening:
                if (now < trial.NextLineUtc)
                    break;
                Say(trial.Lines.Dequeue(), after);
                if (trial.Lines.Count > 0)
                    trial.NextLineUtc = now.AddSeconds(LineSeconds);
                else
                {
                    // The last line was "you have 15 seconds to speak".
                    trial.Phase = Phase.Plea;
                    trial.PhaseEndsUtc = now.AddSeconds(PleaSeconds);
                }
                break;
            case Phase.Plea:
                if (now < trial.PhaseEndsUtc)
                    break;
                Say("The jury will now vote.", after);
                trial.Phase = Phase.Voting;
                trial.PhaseEndsUtc = now.AddSeconds(VoteSeconds);
                SendCards(trial, now, after);
                break;
            case Phase.Voting:
                if (now >= trial.PhaseEndsUtc || AllVoted(trial))
                    Verdict(trial, now, after);
                break;
            case Phase.Verdict:
                if (now >= trial.PhaseEndsUtc)
                    Finish(trial, after);
                break;
        }
    }

    private static void StartSummons(int prisonerId, DateTime now, List<Action> after)
    {
        var trial = new Trial
        {
            PrisonerId = prisonerId,
            Phase = Phase.Summons,
            PhaseEndsUtc = now.AddSeconds(SummonsSeconds)
        };
        foreach (var id in EligibleJurors(prisonerId).OrderBy(_ => Random.Shared.Next()).Take(MaxJurors))
            trial.Picked.Add(id);
        _active = trial;
        foreach (var id in trial.Picked)
            Whisper(id, InviteWhisper, after);
    }

    private static void StepSummons(Trial trial, DateTime now, List<Action> after)
    {
        if (!trial.Reminded && now >= trial.PhaseEndsUtc.AddSeconds(-ReminderSeconds))
        {
            trial.Reminded = true;
            foreach (var id in trial.Picked.Where(id => !InCourt(id)))
                Whisper(id, ReminderWhisper, after);
        }
        if (now < trial.PhaseEndsUtc)
            return;

        // Court starts now (Twist, 2026-10-20) - with whoever is here.
        var prisoner = HabboOf(trial.PrisonerId);
        if (prisoner == null || !JailState.IsJailed(trial.PrisonerId))
        {
            Cancel(trial, trial.Picked, null, false, after);
            return;
        }
        // In cuffs as court starts: off the jury - no vote, no pay, not told
        // (Twist, 2026-10-20).
        var present = trial.Picked.Where(id => InCourt(id) && !PoliceState.IsCuffed(id)).ToList();
        if (present.Count == 0)
        {
            Cancel(trial, Array.Empty<int>(), NoJurorsWhisper, false, after);
            return;
        }
        trial.Jurors = new HashSet<int>(present);
        trial.Sworn = new HashSet<int>(present);
        foreach (var id in present)
            JuryBox[id] = 0;
        after.Add(() => SeatJurors(trial, present));
        trial.Name = prisoner.Username;
        trial.Look = prisoner.Look ?? "";
        trial.Gender = (prisoner.Gender ?? "M").ToUpperInvariant().StartsWith("F") ? "F" : "M";
        trial.Phase = Phase.Arriving;
        trial.PhaseEndsUtc = now.AddSeconds(ArriveSeconds);
        if (prisoner.CurrentRoom?.RoomId == CourthouseRoomId)
        {
            BeginCourt(trial, now, after);
            return;
        }
        // Through the jail's door on a pass to this one room, as staff's
        // :summon takes them (JailState.AllowSummon).
        var prisonerId = trial.PrisonerId;
        after.Add(() =>
        {
            JailState.AllowSummon(prisonerId, CourthouseRoomId);
            prisoner.PrepareRoom(CourthouseRoomId, "", lockOldRoom: true);
        });
    }

    /// <summary>The prisoner is in court: their clock stops, and the judge opens it.</summary>
    private static void BeginCourt(Trial trial, DateTime now, List<Action> after)
    {
        trial.Phase = Phase.Opening;
        var prisonerId = trial.PrisonerId;
        after.Add(() => JailState.Pause(prisonerId));
        trial.Lines.Enqueue($"Court is in session. {trial.Name} is charged with the following crimes:");
        foreach (var line in JailState.ChargeLines(prisonerId))
            trial.Lines.Enqueue(line);
        trial.Lines.Enqueue("Defendant, you have 15 seconds to speak. How do you plead?");
        trial.NextLineUtc = now.AddSeconds(1);
    }

    /// <summary>
    /// A juror is back in a court that stood empty: it carries on where it
    /// stood - the time left for the prisoner to arrive, to plead or to vote
    /// as it was, and the judge's next line a second on.
    /// </summary>
    private static void CarryOn(Trial trial, DateTime now)
    {
        if (trial.EmptySinceUtc is not { } emptySince)
            return;
        trial.EmptySinceUtc = null;
        trial.PhaseEndsUtc += now - emptySince;
        trial.NextLineUtc = now.AddSeconds(1);
    }

    private static bool AllVoted(Trial trial) => trial.Jurors.Where(InCourt).All(trial.Votes.ContainsKey);

    private static void Verdict(Trial trial, DateTime now, List<Action> after)
    {
        var present = trial.Jurors.Where(InCourt).ToList();
        var guilty = present.Count(id => trial.Votes.GetValueOrDefault(id) == VoteGuilty);
        var notGuilty = present.Count(id => trial.Votes.GetValueOrDefault(id) == VoteNotGuilty);
        var acquitted = notGuilty > guilty;
        // The verdict word in bold - the client bolds **marked** words in a
        // bot's chat (Twist, 2026-10-20).
        var line = guilty + notGuilty == 0
            ? $"No votes were cast. {trial.Name} has been found **guilty**. They will serve their remaining sentence."
            : acquitted
                ? $"{trial.Name} has been found **not guilty** by a vote of {notGuilty} to {guilty}. They will be released from prison."
                : guilty == notGuilty
                    ? $"{trial.Name} has been found **guilty** after a tied vote of {guilty} to {notGuilty}. They will serve their remaining sentence."
                    : $"{trial.Name} has been found **guilty** by a vote of {guilty} to {notGuilty}. They will serve their remaining sentence.";
        CloseCards(trial, after);
        Say(line, after);
        var prisonerId = trial.PrisonerId;
        var result = acquitted ? ResultNotGuilty : ResultGuilty;
        after.Add(() => JailState.MarkTrialDone(prisonerId, result));
        // Paid for staying to the verdict, voted or not (Twist, 2026-10-20).
        Pay(present, after);
        trial.Phase = Phase.Verdict;
        trial.PhaseEndsUtc = now.AddSeconds(VerdictPauseSeconds);
    }

    /// <summary>Court is out: nobody is held any more, and the prisoner goes back to the jail.</summary>
    private static void Finish(Trial trial, List<Action> after)
    {
        _active = null;
        FreeJuryBox(after);
        var prisonerId = trial.PrisonerId;
        after.Add(() => BackToJail(prisonerId));
    }

    /// <summary>
    /// Called off. Those in <paramref name="tell"/> are told; the prisoner is
    /// whispered <paramref name="prisonerWhisper"/> if there is one; the request
    /// is spent; and a prisoner already brought to court goes back to the jail
    /// (<paramref name="moveBack"/> - not one logging out or let go). With
    /// <paramref name="payJurors"/>, every juror in court is paid as at a verdict.
    /// </summary>
    private static void Cancel(Trial trial, IEnumerable<int> tell, string? prisonerWhisper, bool moveBack, List<Action> after, bool payJurors = false)
    {
        _active = null;
        FreeJuryBox(after);
        CloseCards(trial, after);
        foreach (var id in tell.ToList())
            Whisper(id, CancelledWhisper, after);
        if (payJurors)
            Pay(trial.Jurors.Where(InCourt).ToList(), after);
        var prisonerId = trial.PrisonerId;
        if (prisonerWhisper != null)
            Whisper(prisonerId, prisonerWhisper, after);
        var brought = trial.Phase >= Phase.Arriving;
        after.Add(() =>
        {
            JailState.MarkTrialDone(prisonerId, ResultCancelled);
            JailState.Resume(prisonerId);
            if (moveBack && brought)
                BackToJail(prisonerId);
        });
    }

    /// <summary>Each of <paramref name="jurorIds"/> gets <see cref="JuryPay"/> coins and a whisper saying so.</summary>
    private static void Pay(List<int> jurorIds, List<Action> after)
    {
        foreach (var jurorId in jurorIds)
        {
            after.Add(() =>
            {
                CityPlayers.AdjustCreditsFor(jurorId, JuryPay, "Jury duty");
                PlusEnvironment.Game.ClientManager.GetClientByUserId(jurorId)?.SendWhisper($"You have been paid {JuryPay}c for jury duty.");
            });
        }
    }

    /// <summary>The prisoner, out of court: their clock runs again, and they are taken back to a jail bed.</summary>
    private static void BackToJail(int prisonerId)
    {
        JailState.Resume(prisonerId);
        var habbo = HabboOf(prisonerId);
        if (habbo == null || !JailState.IsJailed(prisonerId))
            return;
        JailState.SendToJail(habbo, JailState.JailRoomId(), forward: false);
    }

    /// <summary>
    /// Court has started: each juror onto a jury seat, front row first. Nobody
    /// walks onto one (<see cref="IsJurySeat"/>) and a login back onto one goes
    /// to the lobby (<see cref="OnRoomEntered"/>), so they should all be free;
    /// should they not be, a juror already on a jury seat keeps it, and an
    /// empty seat goes before one somebody else is on - the jury comes first.
    /// With more jurors than seats the rest stay where they stand - held all
    /// the same. Each juror's seat is kept on the trial (<see cref="ReturnToSeat"/>).
    /// </summary>
    private static void SeatJurors(Trial trial, List<int> jurorIds)
    {
        if (!PlusEnvironment.Game.RoomManager.TryGetRoom(CourthouseRoomId, out var room) || room == null)
            return;
        var users = room.GetRoomUserManager();
        var map = room.GetGameMap();
        if (users == null || map == null)
            return;
        var seats = JurySeats(room);
        var jurors = jurorIds.Select(id => users.GetRoomUserByHabbo(id)).Where(user => user != null).ToList();
        var taken = new HashSet<Item>();
        var toSeat = new List<RoomUser>();
        foreach (var user in jurors)
        {
            var own = seats.FirstOrDefault(seat => seat.GetX == user.X && seat.GetY == user.Y && !taken.Contains(seat));
            if (own != null)
            {
                taken.Add(own);
                trial.SeatOf[user.UserId] = own.Id;
            }
            else
                toSeat.Add(user);
        }
        foreach (var user in toSeat)
        {
            var free = seats.Where(item => !taken.Contains(item)).ToList();
            var seat = free.FirstOrDefault(item => !map.MapGotUser(new Point(item.GetX, item.GetY))) ?? free.FirstOrDefault();
            if (seat == null)
                return;
            taken.Add(seat);
            trial.SeatOf[user.UserId] = seat.Id;
            PutOnSeat(room, user, seat);
        }
    }

    /// <summary>
    /// A juror back in court after logging out: onto their own jury seat
    /// (Twist, 2026-10-29) - or, should it have gone, the first one free. With
    /// none free they stay where they are, held all the same.
    /// </summary>
    private static void ReturnToSeat(Trial trial, int habboId)
    {
        if (!PlusEnvironment.Game.RoomManager.TryGetRoom(CourthouseRoomId, out var room) || room == null)
            return;
        var user = room.GetRoomUserManager()?.GetRoomUserByHabbo(habboId);
        var map = room.GetGameMap();
        if (user == null || map == null)
            return;
        var seats = JurySeats(room);
        var seat = trial.SeatOf.TryGetValue(habboId, out var seatId) ? seats.FirstOrDefault(item => item.Id == seatId) : null;
        seat ??= seats.FirstOrDefault(item => !map.MapGotUser(new Point(item.GetX, item.GetY)));
        if (seat == null)
            return;
        trial.SeatOf[habboId] = seat.Id;
        PutOnSeat(room, user, seat);
    }

    /// <summary>The Courthouse's jury seats, front row first.</summary>
    private static List<Item> JurySeats(Room room) =>
        (room.GetRoomItemHandler()?.GetFloor ?? Array.Empty<Item>())
        .Where(item => item?.Definition?.ItemName == JurySeatItemName)
        .OrderByDescending(Forward).ThenBy(item => item.GetX).ThenBy(item => item.GetY).ToList();

    private static void PutOnSeat(Room room, RoomUser user, Item seat)
    {
        room.GetGameMap().TeleportToItem(user, seat);
        room.GetRoomUserManager().UpdateUserStatus(user, false);
        user.UpdateNeeded = true;
    }

    /// <summary>
    /// The net under the jury box, every <see cref="SweepSeconds"/>: anybody on
    /// a jury seat the court is not holding there - however they got there -
    /// goes to the lobby, so nobody is ever left stuck in the sealed-off box
    /// (Twist, 2026-10-29).
    /// </summary>
    private static void SweepJurySeats()
    {
        if (!PlusEnvironment.Game.RoomManager.TryGetRoom(CourthouseRoomId, out var room) || room == null)
            return;
        var stuck = room.GetRoomUserManager()?.GetUserList()
            .Where(user => user != null && !user.IsBot && !HeldInJuryBox(user.UserId) && IsJurySeat(room, user.X, user.Y))
            .Select(user => user.UserId).ToList();
        if (stuck is { Count: > 0 })
            ToLobby(stuck);
    }

    /// <summary>Court is out: the jury may walk again, and is taken out of the sealed-off jury box.</summary>
    private static void FreeJuryBox(List<Action> after)
    {
        var jurors = JuryBox.Keys.ToList();
        JuryBox.Clear();
        if (jurors.Count > 0)
            after.Add(() => ToLobby(jurors));
    }

    /// <summary>
    /// Each of <paramref name="habboIds"/> still in the Courthouse onto a lobby
    /// seat picked at random (Twist, 2026-10-29) - an empty one while there is
    /// one, a sofa seating two, and then any. With no lobby seats at all, the door.
    /// Never anybody the court is holding by then - a juror back in court.
    /// </summary>
    private static void ToLobby(List<int> habboIds)
    {
        if (!PlusEnvironment.Game.RoomManager.TryGetRoom(CourthouseRoomId, out var room) || room == null)
            return;
        var users = room.GetRoomUserManager();
        var map = room.GetGameMap();
        var floor = room.GetRoomItemHandler()?.GetFloor;
        if (users == null || map == null || floor == null)
            return;
        var seats = floor.Where(item => item?.Definition != null && LobbySeatItemNames.Contains(item.Definition.ItemName))
            .SelectMany(item => item.GetAffectedTiles.Values.Select(tile => (Item: item, Tile: new Point(tile.X, tile.Y))))
            .ToList();
        foreach (var habboId in habboIds)
        {
            var user = users.GetRoomUserByHabbo(habboId);
            if (user == null || HeldInJuryBox(habboId))
                continue;
            if (seats.Count == 0)
                map.TeleportToTile(user, map.Model.DoorX, map.Model.DoorY, map.Model.DoorZ, map.Model.DoorOrientation);
            else
            {
                var empty = seats.Where(seat => !map.MapGotUser(seat.Tile)).ToList();
                var pool = empty.Count > 0 ? empty : seats;
                var (item, tile) = pool[Random.Shared.Next(pool.Count)];
                map.TeleportToTile(user, tile.X, tile.Y, item.GetZ, item.Rotation);
            }
            users.UpdateUserStatus(user, false);
            user.UpdateNeeded = true;
        }
    }

    /// <summary>How far a seat stands in the way it faces: the front row, facing the court, is the furthest on.</summary>
    private static int Forward(Item seat) => seat.Rotation switch
    {
        0 => -seat.GetY,
        2 => seat.GetX,
        4 => seat.GetY,
        6 => -seat.GetX,
        _ => 0
    };

    // ---- saying and sending ---------------------------------------------------

    /// <summary>The judge - whichever bot is in the Courthouse - says it to the whole room, bots muted or not.</summary>
    private static void Say(string text, List<Action> after) => after.Add(() =>
    {
        if (!PlusEnvironment.Game.RoomManager.TryGetRoom(CourthouseRoomId, out var room) || room == null)
            return;
        var judge = room.GetRoomUserManager()?.GetUserList().FirstOrDefault(unit => unit != null && unit.IsBot && !unit.IsPet);
        if (judge == null)
            return;
        var bubble = judge.BotData?.ChatBubble ?? 0;
        room.SendPacket(new ChatComposer(judge.VirtualId, text, 0, bubble == 0 ? 2 : bubble));
    });

    /// <summary>Every juror in court their vote card as it stands.</summary>
    private static void SendCards(Trial trial, DateTime now, List<Action> after)
    {
        var inCourt = trial.Jurors.Where(InCourt).ToList();
        var voted = inCourt.Count(trial.Votes.ContainsKey);
        var left = Math.Max(0, (int)Math.Ceiling((trial.PhaseEndsUtc - now).TotalSeconds));
        foreach (var id in inCourt)
            Send(id, new RpJuryVoteComposer(true, trial.Name, trial.Look, trial.Gender, left, trial.Votes.GetValueOrDefault(id), voted, inCourt.Count), after);
    }

    private static void CloseCards(Trial trial, List<Action> after)
    {
        if (trial.Phase < Phase.Voting)
            return;
        foreach (var id in trial.Jurors)
            Send(id, new RpJuryVoteComposer(), after);
    }

    private static void Whisper(int habboId, string text, List<Action> after) =>
        after.Add(() => PlusEnvironment.Game.ClientManager.GetClientByUserId(habboId)?.SendWhisper(text));

    private static void Send(int habboId, IServerPacket packet, List<Action> after) =>
        after.Add(() => PlusEnvironment.Game.ClientManager.GetClientByUserId(habboId)?.Send(packet));

    private static void Run(List<Action> after)
    {
        foreach (var action in after)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[JuryTrials] {exception}");
            }
        }
    }

    private static Habbo? HabboOf(int habboId) => PlusEnvironment.Game.ClientManager.GetClientByUserId(habboId)?.GetHabbo();

    /// <summary>Online and in the Courthouse right now.</summary>
    private static bool InCourt(int habboId)
    {
        var habbo = HabboOf(habboId);
        return habbo != null && habbo.InRoom && habbo.CurrentRoom?.RoomId == CourthouseRoomId;
    }
}
