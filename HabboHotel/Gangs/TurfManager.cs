using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Gangs;

/// <summary>
/// pixelrp: turfs - unsafe rooms a gang can claim (221_TurfZones).
///
/// OWNERSHIP is one row per claimed room in `rp_turfs`, cached here by room id
/// (0 = looked up, nobody holds it). It is DISPLAY for now: the group furni in
/// a turf (gld_item, gld_gate) is drawn in the owner's colours, or in a neutral
/// grey pair while it is unclaimed, by <see cref="TryPaint"/> in the item
/// serializer. The furni's own group - items_groups, which a gld_gate's access
/// rules read - is never rewritten, so a claim changes how a room looks and
/// nothing about how it works.
///
/// A CLAIM holds the room. A gang member types :claim and a capture runs for
/// turf.capture.seconds; it completes only while no member of any OTHER gang
/// is in the room - the owners included, which is how a turf is defended - and
/// the claimer stays in the room, conscious and uncuffed. A rival takes a held
/// turf the same way. Captures live in memory only: a restart drops one in
/// progress, which costs a re-type and nothing else.
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

    private const int DefaultCaptureSeconds = 60;

    private static readonly ConcurrentDictionary<uint, int> Owners = new();

    private sealed class Capture
    {
        public int GangId;
        public string GangName = "";
        public int ClaimerId;
        public string ClaimerName = "";
        public DateTime StartedAt;
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

    /// <summary>The gang holding this turf, or 0.</summary>
    public static int OwnerOf(uint roomId)
    {
        if (Owners.TryGetValue(roomId, out var cached))
            return cached;
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        var gangId = connection.QueryFirstOrDefault<int?>(
            "SELECT `gang_id` FROM `rp_turfs` WHERE `room_id` = @roomId LIMIT 1", new { roomId }) ?? 0;
        Owners[roomId] = gangId;
        return gangId;
    }

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
    /// Begin a claim. False when one is already running here - one at a time,
    /// so two gangs cannot race each other to the same room. The caller
    /// (ClaimCommand) has already checked everything about the claimer.
    /// </summary>
    public static bool TryStart(Room room, Habbo claimer, GangUtility.GangMembership gang)
    {
        var capture = new Capture
        {
            GangId = gang.GangId,
            GangName = gang.Name,
            ClaimerId = claimer.Id,
            ClaimerName = claimer.Username,
            StartedAt = DateTime.UtcNow
        };
        capture.GangOf[claimer.Id] = gang.GangId;
        if (!Captures.TryAdd(room.RoomId, capture))
            return false;

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(claimer.Id);
        if (user != null)
            room.SendPacket(new ChatComposer(user.VirtualId, $"*starts claiming this turf for {gang.Name}*", 0, ActionBubble));

        var seconds = CaptureSeconds();
        GangAlert(gang.GangId, $"[Turf]: {claimer.Username} is claiming {room.Name} - hold it for {seconds} seconds.");
        var owner = OwnerOf(room.RoomId);
        if (owner > 0 && owner != gang.GangId)
            GangAlert(owner, $"[Turf]: {gang.Name} is trying to take {room.Name}!");
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

        foreach (var user in users.GetRoomUsers().ToList())
        {
            var habbo = user?.GetClient()?.GetHabbo();
            if (user == null || user.IsBot || habbo == null)
                continue;
            var theirs = capture.GangOf.GetOrAdd(habbo.Id, id => GangUtility.GetGang(id)?.GangId ?? 0);
            if (theirs != 0 && theirs != capture.GangId)
            {
                Fail(room, capture, claimer, $"{habbo.Username} is here to defend it");
                return;
            }
        }

        if ((DateTime.UtcNow - capture.StartedAt).TotalSeconds >= CaptureSeconds())
            Complete(room, capture, claimer);
    }

    private static void Fail(Room room, Capture capture, RoomUser claimer, string why)
    {
        if (!Captures.TryRemove(room.RoomId, out _))
            return;
        if (claimer != null)
            room.SendPacket(new ChatComposer(claimer.VirtualId, $"*loses the claim on this turf - {why}*", 0, ActionBubble));
        GangAlert(capture.GangId, $"[Turf]: The claim on {room.Name} failed - {why}.");
    }

    private static void Complete(Room room, Capture capture, RoomUser claimer)
    {
        if (!Captures.TryRemove(room.RoomId, out _))
            return;
        var previous = OwnerOf(room.RoomId);

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            connection.Execute(
                "REPLACE INTO `rp_turfs` (`room_id`, `gang_id`, `claimed_by`, `claimed_at`) VALUES (@roomId, @gangId, @claimerId, UNIX_TIMESTAMP())",
                new { roomId = room.RoomId, gangId = capture.GangId, claimerId = capture.ClaimerId });
        }
        Owners[room.RoomId] = capture.GangId;
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
        Owners[roomId] = 0;
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
        foreach (var (roomId, capture) in Captures.ToList())
            if (capture.GangId == gangId)
                Captures.TryRemove(roomId, out _);
        foreach (var roomId in rooms)
        {
            Owners[roomId] = 0;
            if (PlusEnvironment.Game.RoomManager.TryGetRoom(roomId, out var room))
                Recolour(room);
        }
    }

    /// <summary>A gang changed its colours: redraw every loaded turf it holds.</summary>
    public static void RecolourTurfsOf(int gangId)
    {
        foreach (var (roomId, owner) in Owners.ToList())
            if (owner == gangId && PlusEnvironment.Game.RoomManager.TryGetRoom(roomId, out var room))
                Recolour(room);
    }

    /// <summary>The room is being unloaded: a claim cannot outlive the room it is in.</summary>
    public static void Forget(uint roomId) => Captures.TryRemove(roomId, out _);

    /// <summary>
    /// Resend every piece of turf-painted furni in the room, so the colours
    /// change in front of everyone without a reload. <see cref="TryPaint"/>
    /// decides what they now show.
    /// </summary>
    public static void Recolour(Room room)
    {
        if (room == null)
            return;
        foreach (var item in room.GetRoomItemHandler().GetFloor.ToList())
            if (item != null && IsPainted(item))
                room.SendPacket(new ObjectUpdateComposer(item));
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

    private static void GangAlert(int gangId, string line)
    {
        foreach (var member in GangManager.GetMembers(gangId))
            PlusEnvironment.Game.ClientManager.GetClientByUserId(member.UserId)?.SendWhisper(line, AlertBubble);
    }
}
