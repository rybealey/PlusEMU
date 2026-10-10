using System.Collections.Concurrent;
using Dapper;
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
/// runs at exactly those moments), and has no close button.
///
/// On a call: Respond claims it (the first officer to press it; the caller is
/// told), and after that the button is Go to room. Once responded, any officer
/// may mark it - once, for good, no confirmation:
/// - Helpful: the caller is paid <see cref="HelpfulReward"/> coins;
/// - Abuse: the caller is charged with 911abuse, filed by the officer who
///   responded.
/// Nobody may respond to, or mark, a call from one of their own characters.
/// </summary>
public static class EmergencyCalls
{
    public const int MarkNone = 0;
    public const int MarkHelpful = 1;
    public const int MarkAbuse = 2;

    public const int ActionRespond = 1;
    public const int ActionGoToRoom = 2;
    public const int ActionHelpful = 3;
    public const int ActionAbuse = 4;

    /// <summary>Coins paid for a call marked helpful.</summary>
    public const int HelpfulReward = 3;

    /// <summary>How many of the newest calls the window holds.</summary>
    public const int WindowSize = 50;

    public const int MaxMessageLength = 200;

    /// <summary>Between two calls from one player.</summary>
    public const int CooldownSeconds = 60;

    private const string AbuseCrime = "911abuse";

    private static readonly ConcurrentDictionary<int, long> LastCall = new();

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
    }

    private const string CallColumns =
        "`id` AS Id, `caller_id` AS CallerId, `caller_name` AS CallerName, `caller_look` AS CallerLook, " +
        "`caller_gender` AS CallerGender, `room_id` AS RoomId, `room_name` AS RoomName, `message` AS Message, " +
        "`created_at` AS CreatedAt, `responder_id` AS ResponderId, `responder_name` AS ResponderName, " +
        "CAST(`mark` AS SIGNED) AS Mark, `marked_by_name` AS MarkedByName";

    /// <summary>The window's calls, newest first.</summary>
    public static List<Call> Newest()
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<Call>($"SELECT {CallColumns} FROM `rp_911_calls` ORDER BY `id` DESC LIMIT {WindowSize}").ToList();
    }

    private static Call? Load(int id)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<Call>($"SELECT {CallColumns} FROM `rp_911_calls` WHERE `id` = @id", new { id });
    }

    /// <summary>:911 - a call from the room the player is in. What to whisper back.</summary>
    public static string Submit(GameClient session, string message)
    {
        var habbo = session.GetHabbo();
        var room = habbo?.CurrentRoom;
        if (habbo == null || room == null)
            return "";
        message = (message ?? "").Trim();
        if (message.Length == 0)
            return "Usage: :911 <what is happening>";
        if (message.Length > MaxMessageLength)
            message = message[..MaxMessageLength];

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
        }

        var officers = Broadcast();
        return officers > 0
            ? "Your 911 call has been sent to the police."
            : "No officers are on duty right now. Your 911 call is in the queue for the next one.";
    }

    /// <summary>One of the window's buttons, from an officer.</summary>
    public static void Act(GameClient session, int callId, int action)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        if (!PoliceUtility.IsOnDutyOfficer(habbo.Id))
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

        switch (action)
        {
            case ActionRespond:
                Respond(session, call);
                return;
            case ActionGoToRoom:
                GoToRoom(session, call);
                return;
            case ActionHelpful:
            case ActionAbuse:
                MarkCall(session, call, action == ActionHelpful ? MarkHelpful : MarkAbuse);
                return;
        }
    }

    private static void Respond(GameClient session, Call call)
    {
        var habbo = session.GetHabbo();
        if (AccountUtility.SameAccount(habbo.Id, call.CallerId))
        {
            Send(session, "That call is from one of your own characters.");
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
        PlusEnvironment.Game.ClientManager.GetClientByUserId(call.CallerId)?
            .SendWhisper($"Officer {habbo.Username} is responding to your 911 call.");
        Broadcast(habbo.Id, $"You responded to {call.CallerName}'s call.");
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
            Send(session, "That call is from one of your own characters.");
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
            caller?.SendWhisper($"Your 911 call was marked helpful. You have been paid {HelpfulReward}c.");
            notice = $"Marked helpful - {call.CallerName} was paid {HelpfulReward}c.";
        }
        else if (ChargeCommand.FileAuto(call.CallerId, AbuseCrime, call.ResponderId))
        {
            WantedUtility.Broadcast();
            caller?.SendWhisper("Your 911 call was marked as abuse. You have been charged with 911 Abuse.");
            notice = $"Marked as abuse - {call.CallerName} was charged with 911 Abuse.";
        }
        else
            notice = $"Marked as abuse. {call.CallerName} already has an open 911 Abuse charge.";
        Broadcast(habbo.Id, notice);
    }

    /// <summary>
    /// Open or close one officer's window: open with the calls while they are an
    /// on-duty officer, closed otherwise.
    /// </summary>
    public static void PushWindow(GameClient? session, bool onDuty)
    {
        if (session?.GetHabbo() == null)
            return;
        session.Send(onDuty ? new RpEmergencyCallsComposer(true, Newest()) : new RpEmergencyCallsComposer(false, new List<Call>()));
    }

    /// <summary>
    /// The queue to every on-duty officer; `noticeFor` alone is also told
    /// `notice`. How many officers it reached.
    /// </summary>
    public static int Broadcast(int noticeFor = 0, string notice = "")
    {
        List<int> officers;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            officers = connection.Query<int>(
                "SELECT e.`user_id` FROM `rp_corporation_employees` e " +
                "JOIN `rp_corporations` c ON c.`id` = e.`corporation_id` " +
                "WHERE e.`on_duty` = 1 AND c.`is_police` = 1").ToList();
        }
        var calls = Newest();
        var reached = 0;
        foreach (var userId in officers.Distinct())
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
            if (client?.GetHabbo() == null)
                continue;
            client.Send(new RpEmergencyCallsComposer(true, calls, userId == noticeFor ? notice : ""));
            reached++;
        }
        return reached;
    }

    private static void Send(GameClient session, string notice) =>
        session.Send(new RpEmergencyCallsComposer(true, Newest(), notice));

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
