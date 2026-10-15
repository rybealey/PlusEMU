using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
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
/// On a call: Respond claims it (the first officer to press it; the caller is
/// not told), and after that the button is Go to room. Once responded, any officer
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
/// its lock. It never closes.
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

        // pixelrp: the request also goes out like a :ca alert (Twist,
        // 2026-10-15) - the corporation-alert bubble, to on-duty police ONLY,
        // the officer asking included, so it is seen even with the window
        // shut. City Government staff still get the request in 911 Dispatch.
        var alert = $"{habbo.Username} is requesting backup at {room.Name}.";
        foreach (var userId in OnDutyResponders(staff: false))
            PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.SendWhisper(alert, CorporationAlertBubble);

        // pixelrp: no whisper (Twist, 2026-10-15) - the room sees the request
        // made, as it sees a 911 call: "*Twist calls for backup*". With no
        // other officer on duty it waits in the queue for the next one.
        var caller = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (caller != null)
            room.SendPacket(new ChatComposer(caller.VirtualId, "*calls for backup*", 0, PoliceUtility.ActionBubbleFor(habbo.Id)));
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
                if (backup)
                    RespondBackup(session, call);
                else
                    Respond(session, call);
                return;
            case ActionGoToRoom:
                if (backup)
                    GoToBackup(session, call);
                else
                    GoToRoom(session, call);
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

    private static void Respond(GameClient session, Call call)
    {
        var habbo = session.GetHabbo();
        if (AccountUtility.SameAccount(habbo.Id, call.CallerId))
        {
            Send(session, "You cannot attend your own call.");
            return;
        }
        int claimed;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            // The WHERE is the race: two officers pressing at once, one wins.
            claimed = connection.Execute(
                "UPDATE `rp_911_calls` SET `responder_id` = @officerId, `responder_name` = @officerName, `responded_at` = UNIX_TIMESTAMP() " +
                "WHERE `id` = @id AND `responder_id` = 0",
                new { officerId = habbo.Id, officerName = habbo.Username, id = call.Id });
        }
        if (claimed == 0)
        {
            Send(session, "Another officer has already responded to that call.");
            return;
        }
        // Nobody is told (Twist, 2026-10-15): not the caller, and not the
        // officer, whose card now reads "Responded by you".
        Broadcast();
    }

    private static void GoToRoom(GameClient session, Call call)
    {
        var habbo = session.GetHabbo();
        if (call.RoomId <= 0)
            return;
        if (habbo.CurrentRoom != null && habbo.CurrentRoom.Id == call.RoomId)
        {
            Send(session, "You are already there.");
            return;
        }
        habbo.PrepareRoom((uint)call.RoomId, "");
    }

    /// <summary>
    /// Respond on a backup request: join its responders (any number may) and
    /// go. Pressed again by an officer already responding, it only goes.
    /// </summary>
    private static void RespondBackup(GameClient session, Call call)
    {
        var habbo = session.GetHabbo();
        if (AccountUtility.SameAccount(habbo.Id, call.CallerId))
        {
            Send(session, "You cannot attend your own call.");
            return;
        }
        // Refused before they are listed: a name on the card is an officer
        // who is actually coming.
        var cannot = CannotTravel(session);
        if (cannot.Length > 0)
        {
            Send(session, cannot);
            return;
        }

        int joined;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            joined = connection.Execute(
                "INSERT IGNORE INTO `rp_911_responders` (`call_id`, `officer_id`, `officer_name`, `responded_at`) " +
                "VALUES (@id, @officerId, @officerName, UNIX_TIMESTAMP())",
                new { id = call.Id, officerId = habbo.Id, officerName = habbo.Username });
        }
        // Nobody is told (Twist, 2026-10-15): not the officer who asked, and
        // not the one responding - their 911 Dispatch cards name everyone
        // responding, and this broadcast is what updates them.
        if (joined > 0)
            Broadcast();
        GoToBackup(session, call);
    }

    /// <summary>
    /// To the room a backup request was called from, at its door - through its
    /// lock, doorbell and user limit (Habbo.BackupEntryRoomId).
    /// </summary>
    private static void GoToBackup(GameClient session, Call call)
    {
        var habbo = session.GetHabbo();
        if (call.RoomId <= 0)
            return;
        if (habbo.CurrentRoom != null && habbo.CurrentRoom.Id == call.RoomId)
        {
            Send(session, "You are already there.");
            return;
        }
        var cannot = CannotTravel(session);
        if (cannot.Length > 0)
        {
            Send(session, cannot);
            return;
        }
        habbo.BackupEntryRoomId = (uint)call.RoomId;
        // From the hotel view there is no room to leave: the client is told to
        // go, and its entry spends the pass (as :summon forwards somebody).
        if (!habbo.InRoom)
            session.SendRoomForward((uint)call.RoomId);
        else
            habbo.PrepareRoom((uint)call.RoomId, "");
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
        string notice;
        if (mark == MarkHelpful)
        {
            CityPlayers.AdjustCreditsFor(call.CallerId, HelpfulReward, "Helpful 911 call");
            caller?.SendWhisper($"Your 911 call was marked helpful. You have been tipped {HelpfulReward}c.");
            notice = $"Marked helpful - {call.CallerName} was paid {HelpfulReward}c.";
        }
        else if (ChargeCommand.FileAuto(call.CallerId, AbuseCrime, call.ResponderId))
        {
            WantedUtility.Broadcast();
            caller?.SendWhisper("Your 911 call was marked as abuse. You have been charged with 911 Abuse.");
            notice = $"Marked as abuse - {call.CallerName} was charged with 911 Abuse.";
        }
        else
            notice = $"Marked as abuse, but the 911 Abuse charge could not be filed - check the 911abuse crime in housekeeping.";
        Broadcast(habbo.Id, notice);
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
