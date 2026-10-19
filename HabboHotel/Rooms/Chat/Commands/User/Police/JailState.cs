using System.Collections.Concurrent;
using System.Drawing;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp jail: who is serving time, how long they have left, and where the
/// jail is.
///
/// A sentence is handed down by :arrest (ArrestCommand) and served in the
/// jail - any room staff have tagged Jail room in the Room tool's Gameplay tab
/// (rooms.rp_jail_room). While serving, a prisoner can walk and talk and
/// nothing else of consequence: no fighting, no police gear, nothing out of
/// the backpack (<see cref="Blocks"/>, RpUseItemEvent), and no way out - every
/// room change goes through Habbo.PrepareRoom, which asks
/// <see cref="MayEnter"/> first. When the time is up they are told, and the
/// door simply stops holding them.
///
/// THE CLOCK ONLY RUNS WHILE THEY ARE IN THE HOTEL, like the wanted list's.
/// Logging out freezes it and logging back in starts it again from the same
/// number - which is why the database keeps seconds LEFT rather than a
/// release time. Written on logout and once a minute while they serve, so a
/// crash costs a prisoner at most that minute. In memory (<see cref="Serving"/>)
/// only while they are online.
///
/// A prisoner who logs back in is returned to the jail by the ordinary login
/// path: it forwards them to the room they logged out in, which was the jail,
/// and PrepareRoom sends anyone else back there.
///
/// Sent to the jail, a prisoner lands on one of its beds (<see cref="SendToJail"/>),
/// an empty one if there is one. A relog puts them back where they were instead.
///
/// STAFF CAN SUMMON A PRISONER OUT (Twist, 2026-09-30). A :summon gives them a
/// pass into that one room (<see cref="AllowSummon"/>); their time keeps
/// running there, and the moment they try to go anywhere else - or log out -
/// the pass is gone and they are sent back to a bed in the jail.
///
/// A JURY TRIAL (Twist, 2026-10-19). A prisoner serving
/// <see cref="TrialMinSentenceSeconds"/> or more may request one, once per
/// sentence (<see cref="RequestTrial"/>), from the Request a Trial card or with
/// :trial - and only while JuryTrials.MinJurors players could be called to a
/// jury. The client shows the card from <see cref="Trial"/>, sent with the
/// countdown and sent again when the number of players crosses that line. The
/// request is recorded on the sentence; the jury side is not built yet.
/// </summary>
public static class JailState
{
    /// <summary>No sentence is longer than this, however long the sheet (Twist, 2026-09-30).</summary>
    public const int MaxSentenceSeconds = 40 * 60;

    /// <summary>The shortest sentence that may ask for a jury trial (Twist, 2026-10-19).</summary>
    public const int TrialMinSentenceSeconds = 15 * 60;

    /// <summary>
    /// ...and the least time it must still have to run (Twist, 2026-10-20):
    /// the 2-minute summons comes off it, so less would run out before court.
    /// </summary>
    public const int TrialMinSecondsLeft = 5 * 60;

    /// <summary>Not serving, or a sentence too short for a trial: no card.</summary>
    public const int TrialNone = 0;
    /// <summary>May request a trial: the card with its Request a Trial button.</summary>
    public const int TrialOpen = 1;
    /// <summary>Asked already this sentence: the card reads "Waiting for a jury".</summary>
    public const int TrialRequested = 2;

    /// <summary>How often a serving prisoner's clock is written back.</summary>
    private const int SaveEverySeconds = 60;

    /// <summary>How often a prisoner who may still ask for a trial has the number of players online checked.</summary>
    private const int TrialCheckSeconds = 5;

    private sealed class Sentence
    {
        public int RowId { get; init; }
        public int SentenceSeconds { get; init; }
        public DateTime ReleaseAtUtc { get; set; }
        public DateTime LastSavedUtc { get; set; }
        /// <summary>A jury trial has been requested for this sentence - its one request.</summary>
        public bool TrialAsked { get; set; }
        /// <summary>That trial is over - a verdict, or called off. Nothing more to ask.</summary>
        public bool TrialDone { get; set; }
        /// <summary>Found not guilty: let go on reaching the jail (<see cref="OnRoomEntered"/>).</summary>
        public bool Acquitted { get; set; }
        /// <summary>The trial state the prisoner was last sent - what their card shows.</summary>
        public int LastSentTrial { get; set; } = TrialNone;
        public DateTime LastTrialCheckUtc { get; set; }
        /// <summary>The charge sheet the sentence was for ("count&lt;TAB&gt;crime" lines).</summary>
        public string Charges { get; init; } = "";
        /// <summary>
        /// Stopped in the Courthouse (Twist, 2026-10-19): the seconds left when it
        /// stopped; null while the clock runs.
        /// </summary>
        public int? PausedLeft { get; set; }

        public int SecondsLeft => PausedLeft ?? (int)Math.Max(0, Math.Ceiling((ReleaseAtUtc - DateTime.UtcNow).TotalSeconds));
    }

    /// <summary>Where this prisoner stands on a trial now (Trial*).</summary>
    private static int TrialOf(int habboId, Sentence sentence) =>
        // In court for it: the card goes (Twist, 2026-10-20).
        sentence.TrialDone || JuryTrials.ArrivedInCourt(habboId) ? TrialNone
        : sentence.TrialAsked ? TrialRequested
        : sentence.SentenceSeconds < TrialMinSentenceSeconds || sentence.SecondsLeft < TrialMinSecondsLeft ? TrialNone
        : JuryTrials.JurorsAvailable(habboId) >= JuryTrials.MinJurors ? TrialOpen
        : TrialNone;

    /// <summary>The prisoner's countdown, with where they stand on a trial - remembered as what their card now shows.</summary>
    private static RpJailComposer PacketFor(int habboId, Sentence sentence)
    {
        sentence.LastSentTrial = TrialOf(habboId, sentence);
        return new RpJailComposer(sentence.SecondsLeft, sentence.SentenceSeconds, sentence.LastSentTrial, sentence.PausedLeft != null);
    }

    /// <summary>Online prisoners: player id -> their sentence.</summary>
    private static readonly ConcurrentDictionary<int, Sentence> Serving = new();

    /// <summary>
    /// Prisoners a staff member has summoned out: player id -> the one room
    /// outside the jail they may be in. Cleared when they go back to the jail,
    /// try to go anywhere else, or log out.
    /// </summary>
    private static readonly ConcurrentDictionary<int, uint> SummonedTo = new();

    /// <summary>
    /// What a prisoner cannot do, by command key. A block list, for the reason
    /// PoliceState.CuffedCannot gives. Walking, talking and the meta commands
    /// stay; what goes is fighting, police work, handing things over, following
    /// somebody out, and clocking in.
    /// </summary>
    private static readonly HashSet<string> JailedCannot = new(StringComparer.OrdinalIgnoreCase)
    {
        // Fighting, and shoving people about.
        "hit", "slap", "push", "spush", "pull",
        // Police work - a jailed officer is a prisoner first.
        "stun", "fb", "ps", "cuff", "uncuff", "escort", "unescort", "charge", "pardon", "arrest",
        // Trading, and handing things over.
        "offer", "sell", "give", "heal",
        // Out of the room behind somebody, and back on the clock.
        "follow", "startwork",
        // Down and off to the hospital (SuicideCommand).
        "suicide"
    };

    public static bool IsJailed(int habboId) => Serving.ContainsKey(habboId);

    /// <summary>An online prisoner's time left in seconds; 0 for anybody not serving.</summary>
    public static int SecondsLeft(int habboId) => Serving.TryGetValue(habboId, out var sentence) ? sentence.SecondsLeft : 0;

    /// <summary>How many online players are serving right now.</summary>
    public static int ServingCount => Serving.Count;

    /// <summary>The online players serving right now.</summary>
    public static IReadOnlyCollection<int> ServingIds => Serving.Keys.ToList();

    /// <summary>
    /// City Panel: a player's time left, online or not. Online it is the live
    /// clock; offline, the saved one (the clock stops at logout).
    /// </summary>
    public static int SecondsLeftAnywhere(int habboId)
    {
        if (Serving.TryGetValue(habboId, out var sentence))
            return sentence.SecondsLeft;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `seconds_left` FROM `rp_jail` WHERE `user_id` = @user AND `released_at` = 0 ORDER BY `id` DESC LIMIT 1");
        dbClient.AddParameter("user", habboId);
        var row = dbClient.GetRow();
        return row == null ? 0 : Math.Max(0, Convert.ToInt32(row["seconds_left"]));
    }

    /// <summary>
    /// City Panel: staff let a prisoner go early. Online through the one
    /// release point (<see cref="Release"/>); offline, the open record is
    /// closed, so their next login finds nothing to resume. False when there
    /// was no sentence to end.
    /// </summary>
    public static bool StaffRelease(int habboId, GameClient? client)
    {
        if (Serving.ContainsKey(habboId))
        {
            Release(habboId, client);
            return true;
        }
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT COUNT(*) FROM `rp_jail` WHERE `user_id` = @user AND `released_at` = 0");
        dbClient.AddParameter("user", habboId);
        if (dbClient.GetInteger() == 0)
            return false;
        dbClient.SetQuery("UPDATE `rp_jail` SET `seconds_left` = 0, `released_at` = UNIX_TIMESTAMP() WHERE `user_id` = @user AND `released_at` = 0");
        dbClient.AddParameter("user", habboId);
        dbClient.RunQuery();
        JuryTrials.OnReleased(habboId);
        return true;
    }

    /// <summary>Where an online prisoner stands on a jury trial (Trial*); <see cref="TrialNone"/> for anybody not serving.</summary>
    public static int Trial(int habboId) => Serving.TryGetValue(habboId, out var sentence) ? TrialOf(habboId, sentence) : TrialNone;

    /// <summary>
    /// A request for a jury trial - the card's Request a Trial button, or
    /// :trial (<paramref name="typed"/>). Recorded on the prisoner's sentence,
    /// once, for a sentence of <see cref="TrialMinSentenceSeconds"/> or more,
    /// while JuryTrials.MinJurors players could be called. The button stands
    /// even if that number dropped the moment it was pressed - the card was
    /// showing (Twist, 2026-10-20); typed, it is checked there and then. A
    /// typed refusal is whispered; the button's card simply shows the truth.
    /// Nothing else happens yet - the jury side is still to be designed - so
    /// the card just reads "Waiting for a jury".
    /// </summary>
    public static void RequestTrial(GameClient session, bool typed)
    {
        var habbo = session?.GetHabbo();
        if (session == null || habbo == null)
            return;
        if (!Serving.TryGetValue(habbo.Id, out var sentence))
        {
            if (typed)
                session.SendWhisper("You can only request a trial while you are in jail.");
            return;
        }
        var enoughJurors = JuryTrials.JurorsAvailable(habbo.Id) >= JuryTrials.MinJurors;
        string? refusal = null;
        var asked = false;
        // Two presses in a row are one request.
        lock (sentence)
        {
            if (sentence.TrialAsked)
                refusal = "You have already requested a trial.";
            else if (sentence.SentenceSeconds < TrialMinSentenceSeconds)
                refusal = "Only a sentence of 15 minutes or more can go to trial.";
            else if (sentence.SecondsLeft < TrialMinSecondsLeft && (typed || sentence.LastSentTrial != TrialOpen))
                refusal = "You don't have enough time left on your sentence for a trial.";
            else if (!enoughJurors && (typed || sentence.LastSentTrial != TrialOpen))
                refusal = "There are not enough players online for a trial.";
            else
            {
                sentence.TrialAsked = true;
                asked = true;
            }
        }
        if (asked)
        {
            using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
            dbClient.SetQuery("UPDATE `rp_jail` SET `trial_requested_at` = UNIX_TIMESTAMP() WHERE `id` = @id AND `trial_requested_at` = 0 LIMIT 1");
            dbClient.AddParameter("id", sentence.RowId);
            dbClient.RunQuery();
            JuryTrials.Enqueue(habbo.Id);
            // Typed, it is confirmed too (Twist, 2026-10-20); the card shows it anyway.
            if (typed)
                session.SendWhisper("Your trial has been requested.");
        }
        else if (typed && refusal != null)
            session.SendWhisper(refusal);
        session.Send(PacketFor(habbo.Id, sentence));
    }

    /// <summary>Whether being in jail stops this command. One gate in CommandManager.</summary>
    public static bool Blocks(int habboId, string commandKey) =>
        IsJailed(habboId) && JailedCannot.Contains(commandKey);

    // ---- where the jail is --------------------------------------------------

    /// <summary>
    /// Every room tagged as the jail, lowest id first. Read from the database
    /// on first use and kept until a tag changes (<see cref="ForgetJailRooms"/>),
    /// because <see cref="MayEnter"/> asks it on every room change a prisoner
    /// makes and the answer almost never moves.
    /// </summary>
    private static volatile uint[]? _jailRooms;

    /// <summary>A room's jail tag was flipped; read them again on next use.</summary>
    public static void ForgetJailRooms() => _jailRooms = null;

    private static uint[] JailRooms()
    {
        var cached = _jailRooms;
        if (cached != null)
            return cached;
        var rooms = new List<uint>();
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT `id` FROM `rooms` WHERE `rp_jail_room` = '1' ORDER BY `id`");
            var table = dbClient.GetTable();
            if (table != null)
            {
                foreach (System.Data.DataRow row in table.Rows)
                    rooms.Add(Convert.ToUInt32(row["id"]));
            }
        }
        var result = rooms.ToArray();
        _jailRooms = result;
        return result;
    }

    public static bool IsJailRoom(uint roomId) => Array.IndexOf(JailRooms(), roomId) >= 0;

    /// <summary>Where a new prisoner is sent: the lowest-numbered jail room, or 0 when there is none.</summary>
    public static uint JailRoomId()
    {
        var rooms = JailRooms();
        return rooms.Length > 0 ? rooms[0] : 0;
    }

    /// <summary>
    /// May this player go into this room? Always, for anybody not serving. A
    /// prisoner only into the jail - any room of it - and otherwise
    /// <paramref name="jailRoomId"/> says where they belong instead.
    ///
    /// With no jail left at all (the last tag taken off) a prisoner is not
    /// held anywhere: their time still runs, but there is no cell to keep them
    /// in, and pinning them to wherever they happen to be would be worse.
    /// </summary>
    public static bool MayEnter(int habboId, uint roomId, out uint jailRoomId)
    {
        jailRoomId = 0;
        if (!IsJailed(habboId))
            return true;
        var rooms = JailRooms();
        if (rooms.Length == 0 || Array.IndexOf(rooms, roomId) >= 0)
        {
            // Back in the jail, so a summons pass has been used up.
            SummonedTo.TryRemove(habboId, out _);
            return true;
        }
        // Summoned here by staff: this room, and only this room.
        if (SummonedTo.TryGetValue(habboId, out var summonedTo) && summonedTo == roomId)
            return true;
        SummonedTo.TryRemove(habboId, out _);
        jailRoomId = rooms[0];
        return false;
    }

    /// <summary>
    /// A staff :summon is taking a prisoner to <paramref name="roomId"/>: let
    /// them in there (see the summary). Nothing for anybody not serving.
    /// Returns whether they were a prisoner, so the summoner can be told.
    /// </summary>
    public static bool AllowSummon(int habboId, uint roomId)
    {
        if (!IsJailed(habboId))
            return false;
        if (IsJailRoom(roomId))
            SummonedTo.TryRemove(habboId, out _);
        else
            SummonedTo[habboId] = roomId;
        return true;
    }

    /// <summary>
    /// A bed in the jail: an empty one if there is one, and otherwise ANY of
    /// them at random (Twist, 2026-09-30) - an occupied bed is still a bed,
    /// since tile overlap makes sharing legal and a full jail is no reason to
    /// leave somebody at the door, and random spreads a full jail's new
    /// arrivals across the cells rather than piling them all onto the first
    /// bed. Any laying surface counts (a bed, a medical bed). Null when the
    /// jail has none at all.
    /// </summary>
    private static Item? PickBed(Room jail)
    {
        var items = jail.GetRoomItemHandler()?.GetFloor;
        if (items == null)
            return null;
        var map = jail.GetGameMap();
        var taken = new List<Item>();
        foreach (var item in items)
        {
            if (item?.Definition == null || !InteractionTypes.IsLayingSurface(item.Definition.InteractionType))
                continue;
            if (map != null && map.MapGotUser(new Point(item.GetX, item.GetY)))
            {
                taken.Add(item);
                continue;
            }
            return item;
        }
        return taken.Count > 0 ? taken[Random.Shared.Next(taken.Count)] : null;
    }

    /// <summary>
    /// Take a prisoner to the jail, onto a bed. <paramref name="forward"/> is
    /// for a client that has to be TOLD to go - one answering a room entry of
    /// its own, which Habbo.PrepareRoom has just refused; otherwise they are
    /// moved directly. Already in the jail room, they are simply put on the
    /// bed. With no bed they arrive at the door, which is still the jail.
    /// </summary>
    public static void SendToJail(Habbo habbo, uint jailRoomId, bool forward)
    {
        if (habbo?.Client == null || jailRoomId == 0)
            return;
        if (!PlusEnvironment.Game.RoomManager.TryLoadRoom(jailRoomId, out var jail) || jail == null)
        {
            // It will not load, so there is no bed to pick; the entry itself
            // fails the same way and says so.
            if (forward)
                habbo.Client.SendRoomForward(jailRoomId);
            else
                habbo.PrepareRoom(jailRoomId, "");
            return;
        }
        var bed = PickBed(jail);
        if (habbo.CurrentRoom != null && habbo.CurrentRoom.Id == jailRoomId)
        {
            var user = jail.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
            if (bed == null || user == null)
                return;
            jail.GetGameMap().TeleportToItem(user, bed);
            jail.GetRoomUserManager()?.UpdateUserStatus(user, false);
            user.UpdateNeeded = true;
            return;
        }
        // The arrival tile is set BEFORE the move, the way :summon and the
        // hospital do it - it is read as the room is entered.
        if (bed != null)
            habbo.PendingRestore = new PendingRoomRestore(jailRoomId, bed.GetX, bed.GetY, bed.Rotation);
        if (forward)
            habbo.Client.SendRoomForward(jailRoomId);
        else
            habbo.PrepareRoom(jailRoomId, "");
    }

    // ---- sentencing and release ---------------------------------------------

    /// <summary>
    /// Start a sentence of <paramref name="seconds"/> for somebody online. The
    /// caller moves them to <paramref name="jailRoomId"/>; this records it,
    /// starts the clock and shows them the countdown. <paramref name="charges"/>
    /// is the sheet it was for (ArrestCommand.ChargeSheet), kept for a trial.
    /// </summary>
    public static void Start(Habbo prisoner, int officerId, uint jailRoomId, int seconds, string charges = "")
    {
        if (prisoner == null || seconds <= 0)
            return;
        int rowId;
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery(
                "INSERT INTO `rp_jail` (`user_id`, `officer_id`, `room_id`, `sentence_seconds`, `seconds_left`, `jailed_at`, `charges`) " +
                "VALUES (@user, @officer, @room, @seconds, @seconds, UNIX_TIMESTAMP(), @charges)");
            dbClient.AddParameter("user", prisoner.Id);
            dbClient.AddParameter("officer", officerId);
            dbClient.AddParameter("room", (int)jailRoomId);
            dbClient.AddParameter("seconds", seconds);
            dbClient.AddParameter("charges", charges ?? "");
            rowId = (int)dbClient.InsertQuery();
        }
        var now = DateTime.UtcNow;
        var sentence = new Sentence
        {
            RowId = rowId,
            SentenceSeconds = seconds,
            ReleaseAtUtc = now.AddSeconds(seconds),
            LastSavedUtc = now,
            Charges = charges ?? ""
        };
        Serving[prisoner.Id] = sentence;
        prisoner.Client?.Send(PacketFor(prisoner.Id, sentence));
        // The prisoner's uniform, from the booking on (UniformManager).
        UniformManager.Refresh(prisoner.Client);
        SendPrisoners(prisoner.CurrentRoom);
    }

    /// <summary>
    /// Time served: stamp the record, let them go, and tell them. Safe from any
    /// thread and for somebody not serving - only the first caller releases.
    /// A prisoner found not guilty is let go here too, told "You are free to
    /// go." instead of the whisper (<paramref name="acquitted"/>). Any trial of
    /// theirs still to come is called off (JuryTrials.OnReleased).
    /// </summary>
    public static void Release(int habboId, GameClient? client, bool acquitted = false)
    {
        SummonedTo.TryRemove(habboId, out _);
        if (!Serving.TryRemove(habboId, out var sentence))
            return;
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `rp_jail` SET `seconds_left` = 0, `released_at` = UNIX_TIMESTAMP() WHERE `id` = @id LIMIT 1");
            dbClient.AddParameter("id", sentence.RowId);
            dbClient.RunQuery();
        }
        client?.Send(new RpJailComposer(0, 0));
        // Not guilty: a system message, not a whisper (Twist, 2026-10-20).
        if (acquitted)
            client?.SendNotification("You are free to go.");
        else
            client?.SendWhisper("You have served your sentence.");
        // Out of the prisoner's uniform - into their rank's, if they are on duty.
        UniformManager.Refresh(client);
        SendPrisoners((client ?? PlusEnvironment.Game.ClientManager.GetClientByUserId(habboId))?.GetHabbo()?.CurrentRoom);
        JuryTrials.OnReleased(habboId);
    }

    // ---- a jury trial (JuryTrials) -------------------------------------------

    /// <summary>
    /// The prisoner has walked into the Courthouse for their trial: their clock
    /// stops there (Twist, 2026-10-19) and their countdown shows it stopped.
    /// </summary>
    public static void Pause(int habboId)
    {
        if (!Serving.TryGetValue(habboId, out var sentence))
            return;
        lock (sentence)
            sentence.PausedLeft ??= sentence.SecondsLeft;
        PlusEnvironment.Game.ClientManager.GetClientByUserId(habboId)?.Send(PacketFor(habboId, sentence));
    }

    /// <summary>Out of court: their clock runs again from where it stopped.</summary>
    public static void Resume(int habboId)
    {
        if (!Serving.TryGetValue(habboId, out var sentence))
            return;
        lock (sentence)
        {
            if (sentence.PausedLeft is not { } left)
                return;
            sentence.ReleaseAtUtc = DateTime.UtcNow.AddSeconds(left);
            sentence.PausedLeft = null;
        }
        PlusEnvironment.Game.ClientManager.GetClientByUserId(habboId)?.Send(PacketFor(habboId, sentence));
    }

    /// <summary>
    /// The judge's lines for an online prisoner's charges - "1 count of
    /// robbery", "3 counts of assault" - in the sheet's order. None for a
    /// sentence booked before charges were kept.
    /// </summary>
    public static List<string> ChargeLines(int habboId)
    {
        var lines = new List<string>();
        if (!Serving.TryGetValue(habboId, out var sentence) || string.IsNullOrEmpty(sentence.Charges))
            return lines;
        foreach (var line in sentence.Charges.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var count) || count <= 0)
                continue;
            lines.Add($"{count} {(count == 1 ? "count" : "counts")} of {parts[1].Trim().ToLowerInvariant()}");
        }
        return lines;
    }

    /// <summary>
    /// The trial is over (JuryTrials.Result*): written to the sentence, its
    /// card taken away, and - not guilty - marked to be let go in the jail.
    /// Online or not.
    /// </summary>
    public static void MarkTrialDone(int habboId, int result)
    {
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `rp_jail` SET `trial_result` = @result WHERE `user_id` = @user AND `released_at` = 0");
            dbClient.AddParameter("result", result);
            dbClient.AddParameter("user", habboId);
            dbClient.RunQuery();
        }
        if (!Serving.TryGetValue(habboId, out var sentence))
            return;
        sentence.TrialDone = true;
        sentence.Acquitted = result == JuryTrials.ResultNotGuilty;
        PlusEnvironment.Game.ClientManager.GetClientByUserId(habboId)?.Send(PacketFor(habboId, sentence));
    }

    // ---- the session --------------------------------------------------------

    /// <summary>
    /// A player has logged in. If they have time left, start their clock again
    /// from where it stopped. From SSOTicketEvent, BEFORE it forwards them to
    /// their last room, so that forward is already held to the jail.
    /// </summary>
    public static void OnLogin(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (session == null || habbo == null)
            return;
        System.Data.DataRow? row;
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery(
                "SELECT `id`, `sentence_seconds`, `seconds_left`, `trial_requested_at`, `trial_result`, `charges` FROM `rp_jail` " +
                "WHERE `user_id` = @user AND `released_at` = 0 ORDER BY `id` DESC LIMIT 1");
            dbClient.AddParameter("user", habbo.Id);
            row = dbClient.GetRow();
        }
        if (row == null)
            return;
        var rowId = Convert.ToInt32(row["id"]);
        var left = Convert.ToInt32(row["seconds_left"]);
        var now = DateTime.UtcNow;
        var sentence = new Sentence
        {
            RowId = rowId,
            SentenceSeconds = Convert.ToInt32(row["sentence_seconds"]),
            ReleaseAtUtc = now.AddSeconds(Math.Max(0, left)),
            LastSavedUtc = now,
            TrialAsked = Convert.ToInt32(row["trial_requested_at"]) > 0,
            TrialDone = Convert.ToInt32(row["trial_result"]) > 0,
            // Found not guilty, then logged out before reaching the jail: let
            // go when they get there - the login forward takes them.
            Acquitted = Convert.ToInt32(row["trial_result"]) == JuryTrials.ResultNotGuilty,
            Charges = Convert.ToString(row["charges"]) ?? ""
        };
        Serving[habbo.Id] = sentence;
        // Served already - the last save landed on zero. Let them go now
        // rather than holding them for a tick they will never see.
        if (left <= 0)
        {
            Release(habbo.Id, session);
            return;
        }
        session.Send(PacketFor(habbo.Id, sentence));
    }

    /// <summary>
    /// A prisoner has logged out: stop their clock where it is. A summons pass
    /// does not outlive the session - they log back in to the jail.
    /// </summary>
    public static void OnLogout(int habboId)
    {
        SummonedTo.TryRemove(habboId, out _);
        if (!Serving.TryRemove(habboId, out var sentence))
            return;
        Save(sentence);
    }

    /// <summary>
    /// Who in this room is serving time, as virtual ids
    /// (RpRoomPrisonersComposer) - the avatar menu leaves Trade off them.
    /// </summary>
    private static RpRoomPrisonersComposer PrisonersIn(Room room) =>
        new(Serving.IsEmpty
            ? new List<int>()
            : room.GetRoomUserManager().GetRoomUsers()
                .Where(unit => unit != null && !unit.IsBot && Serving.ContainsKey(unit.UserId))
                .Select(unit => unit.VirtualId)
                .ToList());

    /// <summary>The room's prisoner list to everybody in it, after somebody in it is sentenced or let go.</summary>
    public static void SendPrisoners(Room? room)
    {
        if (room != null)
            room.SendPacket(PrisonersIn(room));
    }

    /// <summary>
    /// Somebody has arrived in a room. Everybody in it learns the room's
    /// prisoners when the newcomer is one; anybody else is sent the list for
    /// themselves, which also replaces what they knew of the room they left.
    ///
    /// A prisoner - the jail after an arrest, or back in it after a relog - is
    /// shown their countdown there too: the login-time send can land before
    /// the client is listening, and this one cannot.
    /// </summary>
    public static void OnRoomEntered(Room room, RoomUser user)
    {
        if (room == null || user == null || user.IsBot)
            return;
        if (!Serving.TryGetValue(user.UserId, out var sentence))
        {
            user.GetClient()?.Send(PrisonersIn(room));
            return;
        }
        // Found not guilty and back in the jail: free (Twist, 2026-10-20).
        // Off the room's thread - the release writes to the database.
        if (sentence.Acquitted && IsJailRoom(room.RoomId))
        {
            var habboId = user.UserId;
            var client = user.GetClient();
            _ = Task.Run(() => Release(habboId, client, acquitted: true));
            return;
        }
        room.SendPacket(PrisonersIn(room));
        user.GetClient()?.Send(PacketFor(user.UserId, sentence));
        // Back in after a relog: the login set their own look, so dress them
        // again here, where the room is known.
        UniformManager.Refresh(user.GetClient());
    }

    /// <summary>
    /// One room cycle for one player: release anybody whose time is up, write
    /// a serving prisoner's clock back once a minute, and - for a prisoner who
    /// may still ask for a trial - show or hide the Request a Trial card as the
    /// number of players who could serve on a jury crosses
    /// JuryTrials.MinJurors. Free for everybody while nobody is in jail - one
    /// emptiness check.
    ///
    /// Decides only. The release or save it returns writes to the database,
    /// and the room tick calls this holding the lock the movement sender needs
    /// for every room, so the tick runs it once that lock is let go. Null when
    /// there is nothing to do.
    /// </summary>
    public static Action? Tick(RoomUser user)
    {
        if (Serving.IsEmpty || user == null || user.IsBot)
            return null;
        if (!Serving.TryGetValue(user.UserId, out var sentence))
            return null;
        var now = DateTime.UtcNow;
        // Stopped in court: nothing runs out there.
        if (sentence.PausedLeft == null && now >= sentence.ReleaseAtUtc)
        {
            // Release is safe from any thread and lets go only once, so a
            // staff release or a logout landing first leaves this a no-op.
            var userId = user.UserId;
            var client = user.GetClient();
            return () => Release(userId, client);
        }
        var save = (now - sentence.LastSavedUtc).TotalSeconds >= SaveEverySeconds;
        // Under TrialMinSecondsLeft with the card already gone, nothing can bring it back.
        var checkTrial = !sentence.TrialAsked && sentence.SentenceSeconds >= TrialMinSentenceSeconds &&
            (sentence.SecondsLeft >= TrialMinSecondsLeft || sentence.LastSentTrial != TrialNone) &&
            (now - sentence.LastTrialCheckUtc).TotalSeconds >= TrialCheckSeconds;
        if (!save && !checkTrial)
            return null;
        if (save)
            sentence.LastSavedUtc = now;
        if (checkTrial)
            sentence.LastTrialCheckUtc = now;
        var habboId = user.UserId;
        var prisoner = user.GetClient();
        return () =>
        {
            // Logged out in between: OnLogout has already written the clock
            // where it stopped, and this must not land over it.
            if (!Serving.TryGetValue(habboId, out var current) || !ReferenceEquals(current, sentence))
                return;
            if (save)
                Save(sentence);
            if (checkTrial && TrialOf(habboId, sentence) != sentence.LastSentTrial)
                prisoner?.Send(PacketFor(habboId, sentence));
        };
    }

    private static void Save(Sentence sentence)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `rp_jail` SET `seconds_left` = @left WHERE `id` = @id AND `released_at` = 0 LIMIT 1");
        dbClient.AddParameter("left", sentence.SecondsLeft);
        dbClient.AddParameter("id", sentence.RowId);
        dbClient.RunQuery();
    }
}
