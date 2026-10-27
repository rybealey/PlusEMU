using System.Collections.Concurrent;
using System.Drawing;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp (Twist, 2026-10-28): the room behind an arrow gets ready while the
/// player is still walking to it.
///
/// A click whose goal is an arrow (the tile the arrow fires on - RoomUserManager
/// fires it when a walk's goal is the arrow's own tile) queues one job on the
/// room manager's warm worker (WarmInBackground), never on the click's thread:
/// find the twin's room - the links are in memory from startup, a room's furni
/// before the database - and load it if nobody is in it. City rooms are kept
/// loaded (KeyRooms) and cost nothing more. A room warmed for nothing is
/// unloaded like any idle room.
///
/// Then, while they are still heading for that arrow, the player is sent the
/// room's furni types (RpRoomFurniAheadComposer), and their client downloads
/// the ones it does not have yet - so the room draws its furni as it arrives
/// instead of grey placeholders. Once per room per player every
/// SentAgainAfterMs, and FurniTypesMost at most.
///
/// Only a player's click, never a bot's; one job per arrow per player every
/// ClickAgainAfterMs, so a burst of clicks on the same arrow queues one.
/// </summary>
public static class ArrowAhead
{
    private const long ClickAgainAfterMs = 5_000;
    private const long SentAgainAfterMs = 600_000;
    private const int FurniTypesMost = 400;

    /// <summary>Player id -> the arrow they were last warmed for, and when.</summary>
    private static readonly ConcurrentDictionary<int, (uint ArrowId, long At)> Clicked = new();

    /// <summary>Player id -> the room whose furni they were last sent, and when.</summary>
    private static readonly ConcurrentDictionary<int, (uint RoomId, long At)> Sent = new();

    /// <summary>A player's walk was aimed at (x, y) in this room - after the walk itself has gone to movement.</summary>
    public static void OnWalk(Habbo? habbo, Room? room, int x, int y)
    {
        var map = room?.GetGameMap();
        if (habbo == null || room == null || map == null)
            return;
        Item? arrow = null;
        foreach (var item in map.GetCoordinatedItems(new Point(x, y)))
        {
            if (item?.Definition?.InteractionType == InteractionType.Arrow && item.GetX == x && item.GetY == y)
            {
                arrow = item;
                break;
            }
        }
        if (arrow == null)
            return;

        var now = Environment.TickCount64;
        if (Clicked.TryGetValue(habbo.Id, out var last) && last.ArrowId == arrow.Id && (now - last.At) < ClickAgainAfterMs)
            return;
        Clicked[habbo.Id] = (arrow.Id, now);
        StaleEntries.Sweep(Clicked, (_, entry) => (Environment.TickCount64 - entry.At) >= ClickAgainAfterMs);

        var arrowId = arrow.Id;
        PlusEnvironment.Game.RoomManager.WarmInBackground(() =>
        {
            Prepare(habbo, room, arrowId, x, y);
            return Array.Empty<uint>();
        });
    }

    // On the warm worker: the twin's room, loaded, and its furni to the player.
    private static void Prepare(Habbo habbo, Room from, uint arrowId, int x, int y)
    {
        var linked = ItemTeleporterFinder.GetLinkedTele(arrowId);
        if (linked == 0 || from.GetRoomItemHandler().GetItem(linked) != null)
            return;
        var roomId = ItemTeleporterFinder.GetTeleRoomId(linked, from);
        if (roomId == 0 || roomId == from.RoomId)
            return;
        if (!PlusEnvironment.Game.RoomManager.TryLoadRoom(roomId, out var room) || room == null)
            return;

        // still on their way there: in the same room, walking to that arrow
        var user = habbo.CurrentRoom == from ? from.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id) : null;
        if (user == null || user.GoalX != x || user.GoalY != y)
            return;

        var now = Environment.TickCount64;
        if (Sent.TryGetValue(habbo.Id, out var sent) && sent.RoomId == roomId && (now - sent.At) < SentAgainAfterMs)
            return;
        Sent[habbo.Id] = (roomId, now);
        StaleEntries.Sweep(Sent, (_, entry) => (Environment.TickCount64 - entry.At) >= SentAgainAfterMs);

        var floor = new HashSet<int>();
        var wall = new HashSet<int>();
        var items = room.GetRoomItemHandler();
        foreach (var item in items.GetFloor)
        {
            if (item.Definition != null && (floor.Count + wall.Count) < FurniTypesMost)
                floor.Add(item.Definition.SpriteId);
        }
        foreach (var item in items.GetWall)
        {
            if (item.Definition != null && (floor.Count + wall.Count) < FurniTypesMost)
                wall.Add(item.Definition.SpriteId);
        }
        if (floor.Count + wall.Count > 0)
            habbo.Client?.Send(new RpRoomFurniAheadComposer(roomId, floor, wall));
    }
}
