using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: a room change the server makes for somebody - an arrow, a
/// teleporter, a hopper, the hospital collecting a patient - carried out off
/// the thread that decided it.
///
/// WHY. Those are decided on the ONE outbound movement thread (an arrow fires
/// inside RoomUserManager.ApplyMovementFrame, holding the old room's
/// _cycleLock) or on the old room's own tick (teleporters and hoppers; the
/// hospital, inside _cycleLock too). PrepareRoom run there did the whole room
/// change on that thread: leaving the old room (its database writes), loading
/// the new room cold from the database when it was not in memory (a query per
/// bot and pet on top of the furni) and the entry. Every room's walking in the
/// hotel - or the old room's tick - waited for it, and the player looked at a
/// blank screen for the length of the load.
///
/// NO QUEUE. Every trip starts at once on a thread of its own, the way the
/// navigator's OpenFlatConnection runs PrepareRoom on the player's own packet
/// thread. A trip never waits behind somebody else's room load, so nobody is
/// left standing on an arrow waiting for a turn - free to walk off it, and
/// then taken anyway. Cold loads of different rooms still take turns inside
/// RoomManager.TryLoadRoom, but by then the player has already left the old
/// room. A dedicated thread, not the shared pool, for the same reason as
/// RoomTickWriter: the pool can starve on the 2-core VPS.
///
/// ONE TRIP PER PLAYER AT A TIME. A second one while the first is under way
/// is refused: an arrow fires again for anybody still standing on it whenever
/// the room re-runs everyone's status (a furni moved, say), and two
/// PrepareRooms for one player at once would race.
///
/// LEAVING TAKES THE OLD ROOM'S LOCK (PrepareRoom's lockOldRoom). On the
/// movement frame or the tick, the removal could not interleave with the
/// room's own work; from a thread of its own it would, so it takes _cycleLock
/// for just that step - not for the load of the next room.
///
/// A TRIP THAT THROWS IS TRIED ONCE MORE. If it fails while the player is still
/// in the room they set off from, it runs again a moment later; if that fails
/// too, the trip is called off - its teleport flags cleared and the player free
/// to walk. The worst case is an arrow that did nothing this once, never a
/// player left marked as travelling, which every teleporter and wired teleport
/// would then refuse. (One that throws past the door is handled in GiveUp.)
/// </summary>
public static class RoomTransfers
{
    private const int RetryDelayMs = 250;

    /// <summary>Player id -> a trip of theirs is under way.</summary>
    private static readonly ConcurrentDictionary<int, byte> Travelling = new();

    /// <summary>True while a trip of this player's is under way.</summary>
    public static bool IsTravelling(int habboId) => Travelling.ContainsKey(habboId);

    /// <summary>
    /// Send this player through an arrow whose twin is not in the room they are
    /// in. False when a trip of theirs is already under way.
    /// </summary>
    public static bool StartArrow(Habbo? habbo, Room? from, uint linkedArrowId, int facing)
    {
        if (habbo == null || from == null)
            return false;
        return Start(habbo, from,
            () =>
            {
                habbo.IsTeleporting = true;
                habbo.TeleporterId = linkedArrowId;
                habbo.TeleportFacing = facing;
            },
            () => TravelByArrow(habbo, from, linkedArrowId),
            () => CallOff(habbo, from));
    }

    /// <summary>
    /// Send this player from a teleporter booth to its twin in another room.
    /// False when a trip of theirs is already under way.
    /// </summary>
    public static bool StartTeleporter(Habbo? habbo, Room? from, uint linkedTeleId, uint roomId)
    {
        if (habbo == null || from == null)
            return false;
        return Start(habbo, from,
            () =>
            {
                habbo.IsTeleporting = true;
                habbo.TeleportingRoomId = roomId;
                habbo.TeleporterId = linkedTeleId;
            },
            () =>
            {
                if (StillIn(habbo, from))
                    habbo.PrepareRoom(roomId, "", lockOldRoom: true);
                else if (habbo.Client?.GetHabbo() == habbo)
                    habbo.EndTeleport();
            },
            () => CallOff(habbo, from));
    }

    /// <summary>
    /// Send this player from a hopper to a hopper in some other room. Finding
    /// that room is part of the trip. False when a trip of theirs is already
    /// under way.
    /// </summary>
    public static bool StartHopper(Habbo? habbo, Room? from)
    {
        if (habbo == null || from == null)
            return false;
        return Start(habbo, from, null,
            () =>
            {
                if (!StillIn(habbo, from))
                    return;
                var roomHopId = ItemHopperFinder.GetAHopper(from.RoomId);
                habbo.IsHopping = true;
                habbo.HopperId = ItemHopperFinder.GetHopperId(roomHopId);
                habbo.PrepareRoom(roomHopId, "", lockOldRoom: true);
            },
            () => CallOff(habbo, from));
    }

    /// <summary>
    /// Run a trip for this player on a thread of its own. <paramref name="prepare"/>
    /// runs first, here, so the flags it sets are in place before the trip can
    /// start; <paramref name="callOff"/> undoes them when the trip fails twice.
    /// False, and nothing run, when a trip of theirs is already under way.
    /// </summary>
    public static bool Start(Habbo? habbo, Room? from, Action? prepare, Action travel, Action? callOff)
    {
        if (habbo == null || from == null || !Travelling.TryAdd(habbo.Id, 0))
            return false;
        // Not in that room any more - a trip that has just finished took them
        // out of it - so this request is stale. Its prepare would rewrite the
        // flags that trip left for the next room to place them by.
        if (!StillIn(habbo, from))
        {
            Travelling.TryRemove(habbo.Id, out _);
            return false;
        }
        try
        {
            prepare?.Invoke();
            new Thread(() => Run(habbo, from, travel, callOff)) { IsBackground = true, Name = "PixelRPRoomTransfer" }.Start();
            return true;
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
            Try(callOff);
            Travelling.TryRemove(habbo.Id, out _);
            return false;
        }
    }

    private static void Run(Habbo habbo, Room from, Action travel, Action? callOff)
    {
        try
        {
            for (var attempt = 1;; attempt++)
            {
                try
                {
                    travel();
                    return;
                }
                catch (Exception e)
                {
                    ExceptionLogger.LogException(e);
                    // Tried once more only while they are still where they set
                    // off from; past the door there is nothing to try again.
                    if (attempt >= 2 || !StillIn(habbo, from))
                    {
                        GiveUp(habbo, from, callOff);
                        return;
                    }
                }
                Thread.Sleep(RetryDelayMs);
            }
        }
        finally
        {
            Travelling.TryRemove(habbo.Id, out _);
        }
    }

    /// <summary>
    /// A trip that failed for good. Still in the room they set off from: it is
    /// called off. Out of it and into no room - it threw between leaving one
    /// and entering the next: the hotel view, as PrepareRoom's own refusals
    /// end. Already in the next room - it threw after EnterRoom: left alone,
    /// since the teleport flags are what place them as they arrive.
    /// </summary>
    private static void GiveUp(Habbo habbo, Room from, Action? callOff)
    {
        if (habbo.Client?.GetHabbo() != habbo)
            return;
        if (habbo.CurrentRoom == from)
            Try(callOff);
        else if (habbo.CurrentRoom == null)
        {
            habbo.EndTeleport();
            habbo.Client.Send(new CloseConnectionComposer());
        }
    }

    private static void TravelByArrow(Habbo habbo, Room from, uint linkedArrowId)
    {
        // Gone, relogged (a new Habbo), or already somewhere else - the
        // navigator, a summon - before this ran: the trip is off.
        if (!StillIn(habbo, from))
        {
            if (habbo.Client?.GetHabbo() == habbo)
                habbo.EndTeleport();
            return;
        }

        var roomId = ItemTeleporterFinder.GetTeleRoomId(linkedArrowId, from);
        if (roomId == 0)
        {
            // the twin was picked up: the arrow leads nowhere, as an unlinked one
            CallOff(habbo, from);
            return;
        }
        if (roomId == from.RoomId)
        {
            // the database places the twin in this room, but the room has no
            // such item - the old in-room branch's answer
            habbo.EndTeleport();
            habbo.Client?.SendWhisper("Hey, that arrow is poorly!");
            return;
        }

        habbo.TeleportingRoomId = roomId;
        habbo.PrepareRoom(roomId, "", lockOldRoom: true);
    }

    /// <summary>Still this session, and still in the room the trip set off from.</summary>
    private static bool StillIn(Habbo habbo, Room from) =>
        habbo.Client?.GetHabbo() == habbo && habbo.CurrentRoom == from;

    /// <summary>The trip is off: no teleport left pending, and free to walk.</summary>
    private static void CallOff(Habbo habbo, Room from)
    {
        habbo.EndTeleport();
        from.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)?.UnlockWalking();
    }

    private static void Try(Action? work)
    {
        if (work == null)
            return;
        try
        {
            work();
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }
}
