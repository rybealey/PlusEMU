using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: 911 calls (design: the "911 Emergency Calls" canvas).
///
/// Any player calls with :911 &lt;message&gt; (or :999). The call goes into the
/// queue (rp_911_calls, migration 241) whether or not anybody is on duty - the
/// next officer to clock in finds it waiting. Every on-duty officer has the
/// Emergency Calls window open, newest call first; it opens on clock-in and
/// closes on clock-out (PushWindow, from PoliceUtility.PushPardonRights, which
/// runs at exactly those moments). An officer may close it; the next new call
/// brings it back up (the client's doing - every change is broadcast anyway).
///
/// On a call: Respond takes the officer to the room and claims it (the first
/// officer to arrive; the caller is not told), and after that the button is
/// Go to room for every officer. Respond and Go to room stop the officer and
/// take them there after a few seconds' wait (see "the trip" below). Once
/// responded, any officer
/// may mark it - once, for good, no confirmation:
/// - Helpful: the caller is paid <see cref="HelpfulReward"/> coins;
/// - Abuse: the caller is charged with 911abuse, filed by the officer who
///   responded.
/// Nobody may respond to, or mark, a call from one of their own characters,
/// and a player with an open 911 Abuse charge cannot call until it is gone.
///
/// :backup (migration 251) puts a BACKUP REQUEST in the same queue: an
/// on-duty officer (or on-duty staff) asking the others to come, every 3
/// minutes at most.
/// It carries no message and is never marked. Any number of officers respond
/// to one, and responding takes them to the room it was called from, through
/// its lock, after the same wait. It never closes.
/// </summary>
public static class EmergencyCalls
{
    public const int KindCall = 0;
    public const int KindBackup = 1;

    public const int MarkNone = 0;
    public const int MarkHelpful = 1;
    public const int MarkAbuse = 2;

    public const int ActionRespond = 1;
    public const int ActionGoToRoom = 2;
    public const int ActionHelpful = 3;
    public const int ActionAbuse = 4;

    /// <summary>Coins paid for a call marked helpful.</summary>
    public const int HelpfulReward = 3;

    /// <summary>How many of the newest calls (and backup requests) 911 Dispatch holds.</summary>
    public const int WindowSize = 20;

    /// <summary>
    /// How many the database keeps (Twist, 2026-10-13): the newest, older ones
    /// deleted as new ones arrive (<see cref="Prune"/>). Nothing else reads
    /// these tables - a 911 Abuse charge lives in rp_charges on its own.
    /// </summary>
    public const int StoredCalls = 100;

    public const int MaxMessageLength = 200;

    /// <summary>The blue action bubble every narrated action uses.</summary>
    private const int ActionBubble = 4;

    /// <summary>The corporation-alert bubble :ca whispers in (CorporationAlertCommand).</summary>
    private const int CorporationAlertBubble = 11;

    /// <summary>Between two calls from one player.</summary>
    public const int CooldownSeconds = 60;

    /// <summary>Between two backup requests from one officer.</summary>
    public const int BackupCooldownSeconds = 180;

    private const string AbuseCrime = "911abuse";

    private static readonly ConcurrentDictionary<int, long> LastCall = new();
    private static readonly ConcurrentDictionary<int, long> LastBackup = new();

    /// <summary>An officer answering a backup request.</summary>
    public sealed class Responder
    {
        public int CallId { get; set; }
        public int OfficerId { get; set; }
        public string OfficerName { get; set; } = "";
    }

    public sealed class Call
    {
        public int Id { get; set; }
        public int CallerId { get; set; }
        public string CallerName { get; set; } = "";
        public string CallerLook { get; set; } = "";
        public string CallerGender { get; set; } = "M";
        public int RoomId { get; set; }
        public string RoomName { get; set; } = "";
        public string Message { get; set; } = "";
        public int CreatedAt { get; set; }
        public int ResponderId { get; set; }
        public string ResponderName { get; set; } = "";
        public int Mark { get; set; }
        public string MarkedByName { get; set; } = "";
        public int Kind { get; set; }
        /// <summary>A backup request's responders, first to answer first; empty for a 911 call.</summary>
        public List<Responder> Responders { get; set; } = new();
    }

    private const string CallColumns =
        "`id` AS Id, `caller_id` AS CallerId, `caller_name` AS CallerName, `caller_look` AS CallerLook, " +
        "`caller_gender` AS CallerGender, `room_id` AS RoomId, `room_name` AS RoomName, `message` AS Message, " +
        "`created_at` AS CreatedAt, `responder_id` AS ResponderId, `responder_name` AS ResponderName, " +
        "CAST(`mark` AS SIGNED) AS Mark, `marked_by_name` AS MarkedByName, CAST(`kind` AS SIGNED) AS Kind";

    /// <summary>The window's calls, newest first.</summary>
    public static List<Call> Newest()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var calls = connection.Query<Call>($"SELECT {CallColumns} FROM `rp_911_calls` ORDER BY `id` DESC LIMIT {WindowSize}").ToList();
        AttachResponders(connection, calls);
        return calls;
    }

    private static Call? Load(int id)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var call = connection.QueryFirstOrDefault<Call>($"SELECT {CallColumns} FROM `rp_911_calls` WHERE `id` = @id", new { id });
        if (call != null)
            AttachResponders(connection, new List<Call> { call });
        return call;
    }

    /// <summary>
    /// Down to the newest <see cref="StoredCalls"/>, after each one added. The
    /// cut-off is the id of the last one kept: with fewer than that many it is
    /// NULL, and nothing is deleted. The derived table is what lets MySQL read
    /// the table it is deleting from. A pruned call's responders go with it.
    /// </summary>
    private static void Prune(System.Data.IDbConnection connection)
    {
        connection.Execute(
            "DELETE FROM `rp_911_calls` WHERE `id` < (SELECT `id` FROM " +
            $"(SELECT `id` FROM `rp_911_calls` ORDER BY `id` DESC LIMIT 1 OFFSET {StoredCalls - 1}) AS `keep`)");
        connection.Execute(
            "DELETE r FROM `rp_911_responders` r LEFT JOIN `rp_911_calls` c ON c.`id` = r.`call_id` WHERE c.`id` IS NULL");
    }

    private static void AttachResponders(System.Data.IDbConnection connection, List<Call> calls)
    {
        var ids = calls.Where(call => call.Kind == KindBackup).Select(call => call.Id).ToList();
        if (ids.Count == 0)
            return;
        var byCall = connection.Query<Responder>(
                "SELECT `call_id` AS CallId, `officer_id` AS OfficerId, `officer_name` AS OfficerName " +
                "FROM `rp_911_responders` WHERE `call_id` IN @ids ORDER BY `responded_at`, `officer_id`", new { ids })
            .GroupBy(responder => responder.CallId)
            .ToDictionary(group => group.Key, group => group.ToList());
        foreach (var call in calls)
            if (byCall.TryGetValue(call.Id, out var responders))
                call.Responders = responders;
    }

    /// <summary>:911 - a call from the room the player is in. What to whisper back.</summary>
    public static string Submit(GameClient session, string message)
    {
        var habbo = session.GetHabbo();
        var room = habbo?.CurrentRoom;
        if (habbo == null || room == null)
            return "";

        // An officer on duty answers 911 calls, not places them. Asked first,
        // so a bare :911 from one gets this rather than the usage line.
        if (PoliceUtility.IsOnDutyOfficer(habbo.Id))
            return "You cannot call 911 while you are on duty.";

        message = (message ?? "").Trim();
        if (message.Length == 0)
            return "Usage: :911 <what is happening>";
        if (message.Length > MaxMessageLength)
            message = message[..MaxMessageLength];

        // A caller charged with 911 Abuse has lost the line until the charge
        // is gone - pardoned, or lapsed (cleared first, so a lapsed one never
        // blocks).
        WantedUtility.ExpireLapsed();
        if (HasOpenAbuseCharge(habbo.Id))
            return "You cannot call 911 while you are charged with 911 Abuse.";

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (LastCall.TryGetValue(habbo.Id, out var last) && now - last < CooldownSeconds)
            return $"You have just called 911. Try again in {CooldownSeconds - (now - last)} seconds.";
        LastCall[habbo.Id] = now;
        // pixelrp: pruned by age, never at logout - a relog must not skip the cooldown (StaleEntries).
        Plus.Utilities.StaleEntries.Sweep(LastCall, (_, at) => now - at >= CooldownSeconds);

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute(
                "INSERT INTO `rp_911_calls` (`caller_id`, `caller_name`, `caller_look`, `caller_gender`, `room_id`, `room_name`, `message`, `created_at`) " +
                "VALUES (@callerId, @callerName, @look, @gender, @roomId, @roomName, @message, UNIX_TIMESTAMP())",
                new
                {
                    callerId = habbo.Id,
                    callerName = habbo.Username,
                    look = habbo.Look ?? "",
                    gender = (habbo.Gender ?? "M").ToUpperInvariant().StartsWith("F") ? "F" : "M",
                    roomId = (int)room.Id,
                    roomName = Truncate(room.Name ?? "", 64),
                    message
                });
            Prune(connection);
        }

        Broadcast();

        // pixelrp: no whisper either way (Twist, 2026-10-15) - the room sees
        // the call made, the way it sees any other action, whether or not an
        // officer is on duty to take it. Wrapped in "*" so the client narrates
        // it: "*Twist calls police for help*". With nobody on duty the call
        // waits in the queue for the next officer.
        var caller = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (caller != null)
            room.SendPacket(new ChatComposer(caller.VirtualId, "*calls police for help*", 0, ActionBubble));
        return "";
    }

    /// <summary>
    /// :backup - an on-duty officer, or on-duty staff (City Government), asks
    /// the others to come to the room they are in - the same who may use every
    /// other police command (CanActAsOfficer). What to whisper back.
    /// </summary>
    public static string SubmitBackup(GameClient session)
    {
        var habbo = session.GetHabbo();
        var room = habbo?.CurrentRoom;
        if (habbo == null || room == null)
            return "";
        if (!PoliceUtility.CanActAsOfficer(habbo.Id))
            return PoliceUtility.IsOfficer(habbo.Id)
                ? "You have to be on duty to call for backup."
                : "Only police officers can call for backup.";

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (LastBackup.TryGetValue(habbo.Id, out var last) && now - last < BackupCooldownSeconds)
            return $"You have just called for backup. Try again in {BackupCooldownSeconds - (now - last)} seconds.";
        LastBackup[habbo.Id] = now;
        // pixelrp: pruned by age, never at logout - a relog must not skip the cooldown (StaleEntries).
        Plus.Utilities.StaleEntries.Sweep(LastBackup, (_, at) => now - at >= BackupCooldownSeconds);

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute(
                "INSERT INTO `rp_911_calls` (`kind`, `caller_id`, `caller_name`, `caller_look`, `caller_gender`, `room_id`, `room_name`, `message`, `created_at`) " +
                "VALUES (@kind, @callerId, @callerName, @look, @gender, @roomId, @roomName, '', UNIX_TIMESTAMP())",
                new
                {
                    kind = KindBackup,
                    callerId = habbo.Id,
                    callerName = habbo.Username,
                    look = habbo.Look ?? "",
                    gender = (habbo.Gender ?? "M").ToUpperInvariant().StartsWith("F") ? "F" : "M",
                    roomId = (int)room.Id,
                    roomName = Truncate(room.Name ?? "", 64),
                });
            Prune(connection);
        }

        Broadcast();

        // pixelrp: no whisper (Twist, 2026-10-15) - the room sees the request
        // made, as it sees a 911 call: "*Twist calls for backup*". With no
        // other officer on duty it waits in the queue for the next one. Sent
        // BEFORE the alert below, so the bubble comes first (Twist, 2026-10-18).
        var caller = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (caller != null)
            room.SendPacket(new ChatComposer(caller.VirtualId, "*calls for backup*", 0, PoliceUtility.ActionBubbleFor(habbo.Id)));

        // pixelrp: the request also goes out like a :ca alert (Twist,
        // 2026-10-15) - the corporation-alert bubble, to on-duty police ONLY,
        // the officer asking included, so it is seen even with the window
        // shut. City Government staff still get the request in 911 Dispatch.
        var alert = $"{habbo.Username} is requesting backup at {room.Name}.";
        foreach (var userId in OnDutyResponders(staff: false))
            PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.SendWhisper(alert, CorporationAlertBubble);
        return "";
    }

    /// <summary>One of the window's buttons, from an officer.</summary>
    public static void Act(GameClient session, int callId, int action)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        if (!PoliceUtility.CanActAsOfficer(habbo.Id))
        {
            PushWindow(session, false);
            return;
        }
        var call = Load(callId);
        if (call == null)
        {
            Send(session, "That call is no longer in the queue.");
            return;
        }

        var backup = (call.Kind == KindBackup);
        switch (action)
        {
            case ActionRespond:
                StartTrip(session, call, respond: true);
                return;
            case ActionGoToRoom:
                StartTrip(session, call, respond: false);
                return;
            case ActionHelpful:
            case ActionAbuse:
                // Helpful and Abuse judge a caller; a backup request has none.
                if (backup)
                    return;
                MarkCall(session, call, action == ActionHelpful ? MarkHelpful : MarkAbuse);
                return;
        }
    }

    // ---- the trip ------------------------------------------------------------
    //
    // Respond and Go to room - on a 911 call and on a backup request alike - do
    // not move an officer at once (Twist, 2026-10-18). They stop where they
    // stand, the room sees them set off, and TripSeconds later they are there.
    // Walking or clicking away calls it off, and so does somebody else
    // stopping them - hit, slapped, spat on, pushed, pulled, stunned, pepper
    // sprayed or cuffed - each with "*steps out of their police car*". A
    // knockout calls it off quietly, and so does leaving the room some other
    // way first. Respond only lists them on arrival: a name on the card is an
    // officer who actually went. On-duty police wear the blue beacon from the
    // button until a few seconds after they walk in (PoliceBeacon).

    /// <summary>Seconds between the button and the arrival.</summary>
    private const int TripSeconds = 4;

    /// <summary>What the room sees as an officer sets off on a 911 call.</summary>
    private const string EmergencyDeparture = "*switches on the siren and jumps into their police car, racing to an emergency*";

    /// <summary>What the room sees when an officer walks off during the wait.</summary>
    private const string TripCancelled = "*steps out of their police car*";

    private sealed class Trip
    {
        public Trip(int callId, bool respond, Habbo habbo, RoomUser from)
        {
            CallId = callId;
            Respond = respond;
            Habbo = habbo;
            From = from;
        }

        public int CallId { get; }
        /// <summary>Respond (listed on arrival) rather than Go to room.</summary>
        public bool Respond { get; }
        /// <summary>The session they set off in - a relog is a new one.</summary>
        public Habbo Habbo { get; }
        /// <summary>The room user they set off as - leaving the room makes a new one.</summary>
        public RoomUser From { get; }
    }

    /// <summary>Officers on their way, by user id: one trip each.</summary>
    private static readonly ConcurrentDictionary<int, Trip> Trips = new();

    /// <summary>
    /// Respond or Go to room pressed: everything that would refuse the trip is
    /// asked now, and then the officer waits out <see cref="TripSeconds"/>.
    /// </summary>
    private static void StartTrip(GameClient session, Call call, bool respond)
    {
        var habbo = session.GetHabbo();
        if (respond && AccountUtility.SameAccount(habbo.Id, call.CallerId))
        {
            Send(session, "You cannot attend your own call.");
            return;
        }
        if (call.RoomId <= 0)
            return;
        var cannot = CannotTravel(session);
        if (cannot.Length > 0)
        {
            Send(session, cannot);
            return;
        }
        // On the way already: a second press changes nothing.
        if (Trips.ContainsKey(habbo.Id))
            return;

        var room = habbo.CurrentRoom;
        // Already there: nothing to wait for. Respond lists them; Go to room
        // has nothing to do.
        if (room != null && room.Id == call.RoomId)
        {
            if (respond)
                Arrive(session, call, respond: true, travel: false);
            else
                Send(session, "You are already there.");
            return;
        }
        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        // From the hotel view there is no avatar to stop and nobody to see it
        // go: straight there, as before.
        if (user == null)
        {
            Arrive(session, call, respond, travel: true);
            return;
        }

        var trip = new Trip(call.Id, respond, habbo, user);
        if (!Trips.TryAdd(habbo.Id, trip))
            return;
        // Stopped in their tracks - the way a stun stops a walker: on the tile
        // they are stepping onto, never back to the one they left.
        user.ClearMovement(true);
        Rooms.Movement.MovementV2Bridge.Halt(user);
        PoliceBeacon.LightUp(habbo);
        AnnounceDeparture(session, call.Kind == KindBackup ? $"*switches on the siren and speeds off to back up {call.CallerName}*" : EmergencyDeparture);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TripSeconds * 1000);
            try
            {
                FinishTrip(habbo.Id, trip);
            }
            catch (Exception exception)
            {
                Plus.Core.ExceptionLogger.LogException(exception);
            }
        });
    }

    /// <summary>
    /// The wait is over: the trip happens if it is still this one, the officer
    /// is still where they set off from, and nothing has stopped them since.
    /// </summary>
    private static void FinishTrip(int habboId, Trip trip)
    {
        // Called off, or already finished: only the trip that is still waiting.
        if (!Trips.TryRemove(new KeyValuePair<int, Trip>(habboId, trip)))
            return;
        // Whatever stops it here puts the beacon out; Arrive takes it along.
        if (!Depart(habboId, trip))
            PoliceBeacon.PutOut(habboId);
    }

    /// <summary>FinishTrip's last checks, then the arrival. False when the officer does not go.</summary>
    private static bool Depart(int habboId, Trip trip)
    {
        var session = trip.Habbo.Client;
        if (session?.GetHabbo() != trip.Habbo)
            return false;
        var user = trip.Habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habboId);
        if (user == null || !ReferenceEquals(user, trip.From))
            return false;
        if (!PoliceUtility.CanActAsOfficer(habboId) || CannotTravel(session).Length > 0 ||
            PoliceState.IsStunned(habboId) || PoliceState.IsDisoriented(habboId))
            return false;
        var call = Load(trip.CallId);
        if (call == null)
        {
            Send(session, "That call is no longer in the queue.");
            return false;
        }
        Arrive(session, call, trip.Respond, travel: true);
        return true;
    }

    /// <summary>
    /// The officer walked or clicked away during the wait (OwnWalk): the trip
    /// is off, and the room sees them get out of the car.
    /// </summary>
    public static void CancelTripByWalking(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || Trips.IsEmpty || !Trips.TryRemove(habbo.Id, out _))
            return;
        PoliceBeacon.PutOut(habbo.Id);
        AnnounceDeparture(session!, TripCancelled);
    }

    /// <summary>
    /// Somebody else stopped the officer during the wait - hit, slapped, spat
    /// on, pushed, pulled, stunned, pepper sprayed or cuffed: the trip is off,
    /// and the room sees them get out of the car (Twist, 2026-10-18). A beat
    /// later, so it comes after that action's own bubble, which the commands
    /// send after calling this. A knockout calls it off quietly
    /// (<paramref name="announce"/> false) - nobody steps anywhere out cold.
    /// </summary>
    public static void CancelTrip(int habboId, bool announce = true)
    {
        if (Trips.IsEmpty || !Trips.TryRemove(habboId, out var trip))
            return;
        PoliceBeacon.PutOut(habboId);
        if (!announce)
            return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(CancelAfterBubbleMs);
            try
            {
                // Still there to be seen, and on their feet: a hit that has
                // since knocked them out, or a room they have since left, says
                // nothing.
                var session = trip.Habbo.Client;
                if (session?.GetHabbo() != trip.Habbo)
                    return;
                var user = trip.Habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habboId);
                if (user == null || !ReferenceEquals(user, trip.From) || user.RpKnockedOut)
                    return;
                AnnounceDeparture(session, TripCancelled);
            }
            catch (Exception exception)
            {
                Plus.Core.ExceptionLogger.LogException(exception);
            }
        });
    }

    /// <summary>How long the cancel bubble waits for the bubble of whatever stopped them.</summary>
    private const int CancelAfterBubbleMs = 500;

    /// <summary>
    /// Respond lists the officer - a 911 call's one responder (the first to
    /// arrive wins) or one more on a backup request - and then they go.
    /// Nobody is told (Twist, 2026-10-15): their 911 Dispatch cards say it,
    /// and the broadcast is what updates them.
    /// </summary>
    private static void Arrive(GameClient session, Call call, bool respond, bool travel)
    {
        var habbo = session.GetHabbo();
        var backup = (call.Kind == KindBackup);
        if (respond)
        {
            int listed;
            using (var connection = PlusEnvironment.DatabaseManager.Connection())
            {
                listed = backup
                    ? connection.Execute(
                        "INSERT IGNORE INTO `rp_911_responders` (`call_id`, `officer_id`, `officer_name`, `responded_at`) " +
                        "VALUES (@id, @officerId, @officerName, UNIX_TIMESTAMP())",
                        new { id = call.Id, officerId = habbo.Id, officerName = habbo.Username })
                    // The WHERE is the race: two officers arriving at once, one
                    // is the responder. The other still goes.
                    : connection.Execute(
                        "UPDATE `rp_911_calls` SET `responder_id` = @officerId, `responder_name` = @officerName, `responded_at` = UNIX_TIMESTAMP() " +
                        "WHERE `id` = @id AND `responder_id` = 0",
                        new { officerId = habbo.Id, officerName = habbo.Username, id = call.Id });
            }
            if (listed > 0)
                Broadcast();
        }
        if (!travel)
            return;
        // A beacon lit for the trip rides along into the caller's room.
        PoliceBeacon.Arriving(habbo);
        if (backup)
        {
            // To the requesting officer's room at its door - through its lock,
            // doorbell and user limit (Habbo.BackupEntryRoomId). From the hotel
            // view there is no room to leave: the client is told to go, and its
            // entry spends the pass (as :summon forwards somebody).
            habbo.BackupEntryRoomId = (uint)call.RoomId;
            if (!habbo.InRoom)
            {
                session.SendRoomForward((uint)call.RoomId);
                return;
            }
        }
        habbo.PrepareRoom((uint)call.RoomId, "");
    }

    /// <summary>
    /// The officer's action bubble in the room they are in - setting off, or
    /// getting back out of the car.
    /// </summary>
    private static void AnnounceDeparture(GameClient session, string text)
    {
        var habbo = session.GetHabbo();
        var room = habbo?.CurrentRoom;
        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo!.Id);
        if (user != null)
            room!.SendPacket(new ChatComposer(user.VirtualId, text, 0, PoliceUtility.ActionBubbleFor(habbo!.Id)));
    }

    /// <summary>
    /// Why this officer cannot go anywhere right now, or "". Out cold, cuffed
    /// or jailed they cannot move at all; escorting, they would leave their
    /// prisoner behind.
    /// </summary>
    private static string CannotTravel(GameClient session)
    {
        var id = session.GetHabbo().Id;
        if (Rooms.KnockedOut.Is(session))
            return "You cannot respond while you are knocked out.";
        if (PoliceState.IsCuffed(id))
            return "You cannot respond while you are cuffed.";
        if (JailState.IsJailed(id))
            return "You cannot respond while you are in jail.";
        if (PoliceState.IsEscorting(id))
            return "You cannot respond while you are escorting someone.";
        return "";
    }

    private static void MarkCall(GameClient session, Call call, int mark)
    {
        var habbo = session.GetHabbo();
        if (call.ResponderId == 0)
        {
            Send(session, "Respond to the call before you mark it.");
            return;
        }
        if (AccountUtility.SameAccount(habbo.Id, call.CallerId))
        {
            Send(session, "You cannot mark your own call.");
            return;
        }
        int marked;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            // Once, for good: whoever marks first decides.
            marked = connection.Execute(
                "UPDATE `rp_911_calls` SET `mark` = @mark, `marked_by_id` = @officerId, `marked_by_name` = @officerName, `marked_at` = UNIX_TIMESTAMP() " +
                "WHERE `id` = @id AND `mark` = 0 AND `responder_id` > 0",
                new { mark, officerId = habbo.Id, officerName = habbo.Username, id = call.Id });
        }
        if (marked == 0)
        {
            Send(session, "That call has already been marked.");
            return;
        }

        var caller = PlusEnvironment.Game.ClientManager.GetClientByUserId(call.CallerId);
        if (mark == MarkHelpful)
        {
            CityPlayers.AdjustCreditsFor(call.CallerId, HelpfulReward, "Helpful 911 call");
            // Names the officer who marked it, and the tip, in one (Twist, 2026-10-18).
            caller?.SendWhisper($"Your call has been marked as helpful by {habbo.Username}. You have been tipped {HelpfulReward}c for your call.");
        }
        else if (ChargeCommand.FileAuto(call.CallerId, AbuseCrime, call.ResponderId))
        {
            WantedUtility.Broadcast();
            caller?.SendWhisper("Your 911 call was marked as abuse. You have been charged with 911 Abuse.");
        }
        else
        {
            // The 911abuse crime is always there (Twist, 2026-10-18), so this
            // is only for the server log - no officer is shown anything.
            Console.WriteLine($"[EmergencyCalls] call {call.Id} marked abuse but the {AbuseCrime} charge could not be filed");
        }

        // No notice for the marking officer (Twist, 2026-10-18): the force
        // hears every mark in corporation chat - the same alert bubble, to
        // on-duty police only, as a backup request - and an abuse mark stays
        // on the call's card.
        var alert = $"{habbo.Username} has marked {call.CallerName}'s call as {(mark == MarkHelpful ? "helpful" : "abuse")}.";
        foreach (var userId in OnDutyResponders(staff: false))
            PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.SendWhisper(alert, CorporationAlertBubble);
        Broadcast();
    }

    /// <summary>
    /// Open or close one officer's window: open with the calls while they are an
    /// on-duty officer, closed otherwise.
    /// </summary>
    /// <summary>
    /// :calls - the window back up for an on-duty officer (or on-duty staff),
    /// even if they put it away with its X. The caller has checked the duty.
    /// </summary>
    public static void Reopen(GameClient session) =>
        session.Send(new RpEmergencyCallsComposer(true, Newest(), "", reopen: true));

    public static void PushWindow(GameClient? session, bool onDuty)
    {
        if (session?.GetHabbo() == null)
            return;
        session.Send(onDuty ? new RpEmergencyCallsComposer(true, Newest()) : new RpEmergencyCallsComposer(false, new List<Call>()));
    }

    /// <summary>
    /// The queue to every on-duty officer - and on-duty staff (City
    /// Government), who take police calls too; `noticeFor` alone is also told
    /// `notice`. How many it reached.
    /// </summary>
    public static int Broadcast(int noticeFor = 0, string notice = "")
    {
        var calls = Newest();
        var reached = 0;
        foreach (var userId in OnDutyResponders())
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
            if (client?.GetHabbo() == null)
                continue;
            client.Send(new RpEmergencyCallsComposer(true, calls, userId == noticeFor ? notice : ""));
            reached++;
        }
        return reached;
    }

    /// <summary>
    /// Who is on duty right now: police officers, and with `staff` also City
    /// Government, who take police calls too.
    /// </summary>
    private static List<int> OnDutyResponders(bool staff = true)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<int>(
            "SELECT e.`user_id` FROM `rp_corporation_employees` e " +
            "JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
            "WHERE e.`on_duty` = 1 AND (c.`is_police` = 1 OR (@staff = 1 AND c.`service_type` = 'staff'))",
            new { staff = staff ? 1 : 0 }).Distinct().ToList();
    }

    private static void Send(GameClient session, string notice) =>
        session.Send(new RpEmergencyCallsComposer(true, Newest(), notice));

    private static bool HasOpenAbuseCharge(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM `rp_charges` ch JOIN `rp_crimes` c ON c.`id` = ch.`crime_id` " +
            "WHERE ch.`user_id` = @userId AND ch.`dropped_at` = 0 AND c.`key_name` = @crime",
            new { userId, crime = AbuseCrime }) > 0;
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
