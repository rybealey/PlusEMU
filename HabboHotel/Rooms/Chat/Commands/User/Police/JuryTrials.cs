using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
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
/// the trial is called off, as it is when every juror has logged out. Anybody else can walk in and watch; a latecomer is not a juror. The
/// prisoner is brought over (their clock stopping as they arrive) and the
/// judge - whichever bot is in the Courthouse - opens the court and reads the
/// charges, gives the defendant 15 seconds to speak, and puts it to the vote.
/// The jurors and the prisoner cannot leave until it is over, short of logging
/// out.
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
    /// <summary>Between the judge's opening lines, so the charges do not come all at once.</summary>
    private const int LineSeconds = 2;
    private const int PleaSeconds = 15;
    private const int VoteSeconds = 10;
    /// <summary>Between the verdict and the prisoner being taken back, so they hear it.</summary>
    private const int VerdictPauseSeconds = 3;
    private const int TickMs = 1000;
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
        public Dictionary<int, int> Votes { get; } = new();
        public Queue<string> Lines { get; } = new();
        public DateTime NextLineUtc { get; set; }
    }

    private static readonly object Lock = new();

    /// <summary>Prisoners waiting their turn, first come first served.</summary>
    private static readonly List<int> Waiting = new();

    private static Trial? _active;

    private static System.Threading.Timer? _timer;
    private static int _ticking;

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
    /// their vote and their pay; the last one gone, and there is no trial.
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
                    trial.Votes.Remove(habboId);
                    // Every juror logged out: called off (Twist, 2026-10-20).
                    if (!trial.Jurors.Any(InCourt))
                        Cancel(trial, Array.Empty<int>(), NoJurorsWhisper, true, after);
                }
            }
        }
        Run(after);
    }

    /// <summary>The prisoner walked into the Courthouse for their trial: court opens.</summary>
    public static void OnRoomEntered(Room room, RoomUser user)
    {
        if (room == null || user == null || user.IsBot || room.RoomId != CourthouseRoomId)
            return;
        var after = new List<Action>();
        lock (Lock)
        {
            if (_active is { Phase: Phase.Arriving } trial && trial.PrisonerId == user.UserId)
                BeginCourt(trial, DateTime.UtcNow, after);
        }
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
            var after = new List<Action>();
            lock (Lock)
                Step(DateTime.UtcNow, after);
            Run(after);
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
