using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.Utilities;

namespace Plus.HabboHotel.Gangs;

/// <summary>
/// pixelrp: turfs - unsafe rooms a gang can claim (221_TurfZones).
///
/// OWNERSHIP is one row per claimed room in `rp_turfs`, cached here by room id
/// (gang 0 = looked up, nobody holds it). It is DISPLAY for now: the group furni
/// in a turf (gld_item, gld_gate) is drawn in the owner's colours, or in a
/// neutral grey pair while it is unclaimed, by <see cref="TryPaint"/> in the
/// item serializer. The furni's own group - items_groups, which a gld_gate's
/// access rules read - is never rewritten, so a claim changes how a room looks
/// and nothing about how it works.
///
/// A CLAIM holds the room for turf.capture.seconds (five minutes) of
/// UNCONTESTED time. A member of any OTHER gang in the room - the owners
/// included, which is how a turf is defended - CONTESTS it: the clock stops
/// where it is and starts again, from there, once every one of them has left.
/// A claim FAILS only if the claimer leaves, is knocked out or is cuffed. A
/// rival takes a held turf the same way. Captures live in memory only: a
/// restart drops one in progress, which costs a re-claim and nothing else.
///
/// Every change is pushed to everyone in the room (<see cref="Broadcast"/>) as
/// the turf panel's state; the client runs the countdown between pushes.
/// </summary>
public static class TurfManager
{
    /// <summary>The unclaimed pair, from the Gang window palette (GANG_COLOURS).</summary>
    public const string NeutralColourA = "b8b8b8";
    public const string NeutralColourB = "444444";

    /// <summary>Gang alerts' private green bubble (GangAlertCommand).</summary>
    private const int AlertBubble = 200;

    /// <summary>The blue action bubble the fight commands use.</summary>
    private const int ActionBubble = 4;

    private const int DefaultCaptureSeconds = 300;

    private readonly record struct Holding(int GangId, int ClaimedAt);

    // A property class, not a positional record or tuple: Dapper binds those
    // only on an exact column-type match (see GangManager's row classes).
    private sealed class TurfRow
    {
        public int GangId { get; set; }
        public int ClaimedAt { get; set; }
    }

    private static readonly ConcurrentDictionary<uint, Holding> Owners = new();

    private sealed class Capture
    {
        public int GangId;
        public string GangName = "";
        public string GangColourA = NeutralColourA;
        public int ClaimerId;
        public string ClaimerName = "";
        /// <summary>Held, uncontested time so far. Only ever grows while nobody contests it.</summary>
        public double ElapsedMs;
        public DateTime LastTick;
        public bool Contested;
        public string ContestedBy = "";
        /// <summary>
        /// Each user's gang, looked up ONCE per capture: Tick runs every room
        /// cycle, and a query per user per tick is not a price worth paying to
        /// notice somebody changing gangs mid-capture.
        /// </summary>
        public readonly ConcurrentDictionary<int, int> GangOf = new();
    }

    private static readonly ConcurrentDictionary<uint, Capture> Captures = new();

    public static int CaptureSeconds() =>
        int.TryParse(PlusEnvironment.SettingsManager.TryGetValue("turf.capture.seconds"), out var seconds) && seconds > 0
            ? seconds
            : DefaultCaptureSeconds;

    private static Holding HoldingOf(uint roomId)
    {
        if (Owners.TryGetValue(roomId, out var cached))
            return cached;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var row = connection.QueryFirstOrDefault<TurfRow>(
            "SELECT `gang_id` AS GangId, `claimed_at` AS ClaimedAt FROM `rp_turfs` WHERE `room_id` = @roomId LIMIT 1", new { roomId });
        var holding = row != null ? new Holding(row.GangId, row.ClaimedAt) : new Holding(0, 0);
        Owners[roomId] = holding;
        return holding;
    }

    /// <summary>The gang holding this turf, or 0.</summary>
    public static int OwnerOf(uint roomId) => HoldingOf(roomId).GangId;

    /// <summary>The gang holding this turf, by name, or "".</summary>
    public static string OwnerName(uint roomId)
    {
        var owner = OwnerOf(roomId);
        return owner > 0 ? (GangManager.GetGang(owner)?.Name ?? "") : "";
    }

    /// <summary>Is a claim running in this room, and whose?</summary>
    public static bool IsCapturing(uint roomId, out string gangName)
    {
        gangName = Captures.TryGetValue(roomId, out var capture) ? capture.GangName : "";
        return capture != null;
    }

    /// <summary>
    /// A player asks to claim this turf - the turf panel's Claim button
    /// (RpTurfClaimEvent), the only way in now that :claim is gone.
    /// Everything about WHO may start a claim is checked here; everything about
    /// how it runs, in Tick. Refusals are whispered.
    /// </summary>
    public static void TryClaim(GameClient session, Room room)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || room == null)
            return;
        if (!room.IsTurf)
        {
            session.SendWhisper("This room isn't a turf.");
            return;
        }
        if (KnockedOut.Refuse(session))
            return;
        if (Rooms.Chat.Commands.User.Police.PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("Your hands are cuffed.");
            return;
        }

        var gang = GangUtility.GetGang(habbo.Id);
        if (gang == null)
        {
            session.SendWhisper("You're not in a gang.");
            return;
        }
        if (OwnerOf(room.RoomId) == gang.GangId)
        {
            session.SendWhisper("Your gang already holds this turf.");
            return;
        }

        // A turf is an unsafe room, and a passive player is one nobody can
        // fight - so they cannot be the one holding it either.
        habbo.EnsureRpStatsLoaded();
        if (habbo.IsRpPassive)
        {
            session.SendWhisper("You can't claim turf while you're passive.");
            return;
        }

        if (!TryStart(room, habbo, gang))
        {
            IsCapturing(room.RoomId, out var claiming);
            session.SendWhisper($"{claiming} is already claiming this turf.");
        }
    }

    /// <summary>
    /// Begin a claim. False when one is already running here - one at a time,
    /// so two gangs cannot race each other to the same room. The caller
    /// (TryClaim) has already checked everything about the claimer.
    /// </summary>
    public static bool TryStart(Room room, Habbo claimer, GangUtility.GangMembership gang)
    {
        var capture = new Capture
        {
            GangId = gang.GangId,
            GangName = gang.Name,
            GangColourA = Hex(gang.Colour1),
            ClaimerId = claimer.Id,
            ClaimerName = claimer.Username,
            LastTick = DateTime.UtcNow
        };
        capture.GangOf[claimer.Id] = gang.GangId;
        if (!Captures.TryAdd(room.RoomId, capture))
            return false;

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(claimer.Id);
        if (user != null)
            room.SendPacket(new ChatComposer(user.VirtualId, $"*starts claiming this turf for {gang.Name}*", 0, ActionBubble));

        GangAlert(gang.GangId, $"[Turf]: {claimer.Username} is claiming {room.Name} - hold it for {Clock(CaptureSeconds())}.");
        var owner = OwnerOf(room.RoomId);
        if (owner > 0 && owner != gang.GangId)
            GangAlert(owner, $"[Turf]: {gang.Name} is trying to take {room.Name}!");
        Broadcast(room);
        return true;
    }

    /// <summary>
    /// The room cycle's turn (Room.ProcessRoom). One dictionary probe for every
    /// room with no claim running, which is nearly all of them.
    /// </summary>
    public static void Tick(Room room)
    {
        if (Captures.IsEmpty || room == null || !Captures.TryGetValue(room.RoomId, out var capture))
            return;

        var now = DateTime.UtcNow;
        var sinceLast = (now - capture.LastTick).TotalMilliseconds;
        capture.LastTick = now;

        if (!room.IsTurf)
        {
            Fail(room, capture, null, "this room is no longer a turf");
            return;
        }

        var users = room.GetRoomUserManager();
        var claimer = users.GetRoomUserByHabbo(capture.ClaimerId);
        if (claimer == null)
        {
            Fail(room, capture, null, $"{capture.ClaimerName} left the turf");
            return;
        }
        if (claimer.RpKnockedOut)
        {
            Fail(room, capture, claimer, $"{capture.ClaimerName} was knocked out");
            return;
        }
        if (Rooms.Chat.Commands.User.Police.PoliceState.IsCuffed(capture.ClaimerId))
        {
            Fail(room, capture, claimer, $"{capture.ClaimerName} was cuffed");
            return;
        }

        // Any member of another gang here contests the claim. Their gangs are
        // named once, on the moment it becomes contested.
        string rivalName = null;
        var rivalGangs = new HashSet<int>();
        foreach (var user in users.GetRoomUsers().ToList())
        {
            var habbo = user?.GetClient()?.GetHabbo();
            if (user == null || user.IsBot || habbo == null)
                continue;
            var theirs = capture.GangOf.GetOrAdd(habbo.Id, id => GangUtility.GetGang(id)?.GangId ?? 0);
            if (theirs == 0 || theirs == capture.GangId)
                continue;
            rivalName ??= habbo.Username;
            rivalGangs.Add(theirs);
        }

        if (rivalName != null)
        {
            if (!capture.Contested)
            {
                capture.Contested = true;
                capture.ContestedBy = rivalName;
                room.SendPacket(new ChatComposer(claimer.VirtualId, $"*claim contested - {rivalName} is here*", 0, ActionBubble));
                GangAlert(capture.GangId, $"[Turf]: The claim on {room.Name} is contested - it pauses until the rivals leave.");
                foreach (var rival in rivalGangs)
                    GangAlert(rival, $"[Turf]: Your gang is contesting {capture.GangName}'s claim on {room.Name} - stay in the room to hold it off.");
                Broadcast(room);
            }
            return;
        }

        if (capture.Contested)
        {
            capture.Contested = false;
            capture.ContestedBy = "";
            room.SendPacket(new ChatComposer(claimer.VirtualId, "*the claim on this turf continues*", 0, ActionBubble));
            GangAlert(capture.GangId, $"[Turf]: The rivals left {room.Name} - the claim continues.");
            Broadcast(room);
            return; // this tick's time went to a contested room
        }

        capture.ElapsedMs += sinceLast;
        if (capture.ElapsedMs >= CaptureSeconds() * 1000.0)
            Complete(room, capture, claimer);
    }

    private static void Fail(Room room, Capture capture, RoomUser claimer, string why)
    {
        if (!Captures.TryRemove(room.RoomId, out _))
            return;
        if (claimer != null)
            room.SendPacket(new ChatComposer(claimer.VirtualId, $"*loses the claim on this turf - {why}*", 0, ActionBubble));
        GangAlert(capture.GangId, $"[Turf]: The claim on {room.Name} failed - {why}.");
        Broadcast(room, $"Claim failed: {why}.");
    }

    private static void Complete(Room room, Capture capture, RoomUser claimer)
    {
        if (!Captures.TryRemove(room.RoomId, out _))
            return;
        var previous = OwnerOf(room.RoomId);
        var now = (int)UnixTimestamp.GetNow();

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute(
                "REPLACE INTO `rp_turfs` (`room_id`, `gang_id`, `claimed_by`, `claimed_at`) VALUES (@roomId, @gangId, @claimerId, @now)",
                new { roomId = room.RoomId, gangId = capture.GangId, claimerId = capture.ClaimerId, now });
        }
        Owners[room.RoomId] = new Holding(capture.GangId, now);
        Recolour(room);

        room.SendPacket(new ChatComposer(claimer.VirtualId, $"*claims this turf for {capture.GangName}*", 0, ActionBubble));
        GangAlert(capture.GangId, $"[Turf]: {capture.ClaimerName} claimed {room.Name} for the gang.");
        if (previous > 0 && previous != capture.GangId)
            GangAlert(previous, $"[Turf]: {capture.GangName} took {room.Name} from you.");
    }

    /// <summary>
    /// Nobody holds this turf any more: it stopped being one, or its gang is
    /// gone. Any claim running here ends with it.
    /// </summary>
    public static void Release(uint roomId)
    {
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            connection.Execute("DELETE FROM `rp_turfs` WHERE `room_id` = @roomId", new { roomId });
        Owners[roomId] = new Holding(0, 0);
        Captures.TryRemove(roomId, out _);
        if (PlusEnvironment.Game.RoomManager.TryGetRoom(roomId, out var room))
            Recolour(room);
    }

    /// <summary>A gang disbanded: every turf it held goes back to unclaimed.</summary>
    public static void ReleaseAllOf(int gangId)
    {
        List<uint> rooms;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            // long, then narrowed: room_id is a signed int(11), and Dapper will not
            // cast a signed column straight to uint.
            rooms = connection.Query<long>("SELECT `room_id` FROM `rp_turfs` WHERE `gang_id` = @gangId", new { gangId }).Select(id => (uint)id).ToList();
            connection.Execute("DELETE FROM `rp_turfs` WHERE `gang_id` = @gangId", new { gangId });
        }
        var touched = new HashSet<uint>(rooms);
        foreach (var (roomId, capture) in Captures.ToList())
        {
            if (capture.GangId != gangId)
                continue;
            Captures.TryRemove(roomId, out _);
            touched.Add(roomId);
        }
        foreach (var roomId in rooms)
            Owners[roomId] = new Holding(0, 0);
        foreach (var roomId in touched)
            if (PlusEnvironment.Game.RoomManager.TryGetRoom(roomId, out var room))
                Recolour(room);
    }

    /// <summary>A gang changed its colours: redraw every loaded turf it holds or is claiming.</summary>
    public static void RecolourTurfsOf(int gangId)
    {
        var colour = PlusEnvironment.Game.GroupManager.TryGetGroup(gangId, out var gang) ? Hex(gang.Colour1) : null;
        foreach (var (roomId, capture) in Captures.ToList())
            if (capture.GangId == gangId && colour != null)
                capture.GangColourA = colour;
        foreach (var (roomId, holding) in Owners.ToList())
            if (holding.GangId == gangId && PlusEnvironment.Game.RoomManager.TryGetRoom(roomId, out var room))
                Recolour(room);
        foreach (var (roomId, capture) in Captures.ToList())
            if (capture.GangId == gangId && PlusEnvironment.Game.RoomManager.TryGetRoom(roomId, out var room))
                Broadcast(room);
    }

    /// <summary>
    /// A player's gang changed (GangUtility.BroadcastGangMembership - founded,
    /// joined, left, kicked, disbanded). Three things can be stale:
    ///
    /// - their turf panel, which words its button from their gang: resent if
    ///   they are standing in a turf, so "Join a gang to claim" becomes a Claim
    ///   button (or back) in real time;
    /// - every running claim's cached gang for them (Capture.GangOf), dropped so
    ///   the next tick looks them up again - joining a rival gang mid-claim now
    ///   contests it, leaving one stops contesting;
    /// - a claim they are MAKING, which is their gang's: it fails if they are no
    ///   longer in that gang, since nobody would be holding the room for it.
    /// </summary>
    public static void OnMembershipChanged(int userId, int gangId)
    {
        foreach (var (roomId, capture) in Captures.ToList())
        {
            capture.GangOf.TryRemove(userId, out _);
            if (capture.ClaimerId != userId || gangId == capture.GangId)
                continue;
            if (!PlusEnvironment.Game.RoomManager.TryGetRoom(roomId, out var claimRoom))
            {
                Captures.TryRemove(roomId, out _);
                continue;
            }
            Fail(claimRoom, capture, claimRoom.GetRoomUserManager().GetRoomUserByHabbo(userId), $"{capture.ClaimerName} left {capture.GangName}");
        }

        var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
        var room = client?.GetHabbo()?.CurrentRoom;
        if (room != null && room.IsTurf)
            client.Send(new RpRoomTurfComposer(Describe(room, userId)));
    }

    /// <summary>The room is being unloaded: a claim cannot outlive the room it is in.</summary>
    public static void Forget(uint roomId) => Captures.TryRemove(roomId, out _);

    /// <summary>
    /// Resend every piece of turf-painted furni in the room, so the colours
    /// change in front of everyone without a reload, and push the panel state
    /// that goes with them. <see cref="TryPaint"/> decides what they now show.
    /// </summary>
    public static void Recolour(Room room)
    {
        if (room == null)
            return;
        foreach (var item in room.GetRoomItemHandler().GetFloor.ToList())
            if (item != null && IsPainted(item))
                room.SendPacket(new ObjectUpdateComposer(item));
        Broadcast(room);
    }

    /// <summary>
    /// The turf panel's state to everyone in the room. Per viewer, because the
    /// panel's button depends on whose gang they are in; only sent on a change,
    /// so the lookup per viewer is paid rarely.
    /// </summary>
    public static void Broadcast(Room room, string failReason = "")
    {
        if (room == null)
            return;
        foreach (var user in room.GetRoomUserManager().GetRoomUsers().ToList())
        {
            var client = user?.GetClient();
            if (user == null || user.IsBot || client?.GetHabbo() == null)
                continue;
            client.Send(new RpRoomTurfComposer(Describe(room, client.GetHabbo().Id, failReason)));
        }
    }

    /// <summary>The panel state for one viewer - room entry, settings, and every broadcast.</summary>
    public static TurfView Describe(Room room, int viewerId, string failReason = "")
    {
        if (room == null || !room.IsTurf)
            return new TurfView(room?.RoomId ?? 0, false, 0, "", NeutralColourA, NeutralColourB, 0,
                false, "", 0, "", NeutralColourA, 0, CaptureSeconds(), false, "", "", 0);

        var holding = HoldingOf(room.RoomId);
        var ownerName = "";
        var colourA = NeutralColourA;
        var colourB = NeutralColourB;
        if (holding.GangId > 0 && PlusEnvironment.Game.GroupManager.TryGetGroup(holding.GangId, out var owner))
        {
            ownerName = owner.Name;
            colourA = Hex(owner.Colour1);
            colourB = Hex(owner.Colour2);
        }
        var heldFor = holding.GangId > 0 && holding.ClaimedAt > 0 ? Math.Max(0, (int)UnixTimestamp.GetNow() - holding.ClaimedAt) : 0;
        var viewerGang = viewerId > 0 ? (GangUtility.GetGang(viewerId)?.GangId ?? 0) : 0;

        if (!Captures.TryGetValue(room.RoomId, out var capture))
            return new TurfView(room.RoomId, true, holding.GangId, ownerName, colourA, colourB, heldFor,
                false, "", 0, "", NeutralColourA, 0, CaptureSeconds(), false, "", failReason, viewerGang);

        return new TurfView(room.RoomId, true, holding.GangId, ownerName, colourA, colourB, heldFor,
            true, capture.ClaimerName, capture.GangId, capture.GangName, capture.GangColourA,
            (int)(capture.ElapsedMs / 1000), CaptureSeconds(), capture.Contested, capture.ContestedBy, failReason, viewerGang);
    }

    private static bool IsPainted(Item item) =>
        item.Definition != null &&
        (item.Definition.InteractionType == InteractionType.GuildItem || item.Definition.InteractionType == InteractionType.GuildGate);

    /// <summary>
    /// For the item serializer: when this group furni stands in a turf, what it
    /// shows - the owning gang's id, badge and colours, or the neutral pair
    /// with no group while nobody holds it. False anywhere that is not a turf,
    /// and for anything that is not turf-painted: the furni's own group applies.
    /// </summary>
    public static bool TryPaint(Item item, out string groupId, out string badge, out string colourA, out string colourB)
    {
        groupId = "0";
        badge = "";
        colourA = NeutralColourA;
        colourB = NeutralColourB;
        if (item == null || !IsPainted(item))
            return false;
        var room = item.GetRoom();
        if (room == null || !room.IsTurf)
            return false;
        var owner = OwnerOf(room.RoomId);
        if (owner > 0 && PlusEnvironment.Game.GroupManager.TryGetGroup(owner, out var gang))
        {
            groupId = gang.Id.ToString();
            badge = gang.Badge ?? "";
            colourA = Hex(gang.Colour1);
            colourB = Hex(gang.Colour2);
        }
        return true;
    }

    /// <summary>A gang colour (raw RGB) as the hex the group furni visualisation reads - no '#'.</summary>
    public static string Hex(int colour) => (colour & 0xFFFFFF).ToString("x6");

    private static string Clock(int seconds) => seconds >= 60 && seconds % 60 == 0
        ? $"{seconds / 60} minute{(seconds == 60 ? "" : "s")}"
        : $"{seconds / 60}:{seconds % 60:00}";

    private static void GangAlert(int gangId, string line)
    {
        foreach (var member in GangManager.GetMembers(gangId))
            PlusEnvironment.Game.ClientManager.GetClientByUserId(member.UserId)?.SendWhisper(line, AlertBubble);
    }
}

/// <summary>
/// pixelrp: what the turf panel shows (RpRoomTurfComposer). Colours are hex
/// without '#'. ElapsedSeconds is held, uncontested time; the client counts on
/// from it while the claim is running and not contested.
/// </summary>
public readonly record struct TurfView(
    uint RoomId, bool IsTurf, int OwnerGangId, string OwnerName, string OwnerColourA, string OwnerColourB, int HeldForSeconds,
    bool Capturing, string ClaimerName, int ClaimGangId, string ClaimGangName, string ClaimColourA,
    int ElapsedSeconds, int TotalSeconds, bool Contested, string ContestedBy, string FailReason, int ViewerGangId);
