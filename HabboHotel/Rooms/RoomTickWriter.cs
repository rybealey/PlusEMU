using System.Collections.Concurrent;
using Dapper;
using Plus.Core;
using Plus.HabboHotel.Users;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// Database writes the 500ms room tick used to make while holding
/// RoomUserManager's _cycleLock: RP stat saves (passive countdown, snacks and
/// medkits) and the room's user count.
///
/// WHY. The one outbound movement thread takes that same lock to apply and
/// send every room's movement frames. A slow write in one room's tick kept the
/// lock, so the sender waited - and every OTHER room's steps, turns and stops
/// queued behind it. Position saves were moved off the lock for the same
/// reason after 124-991ms stalls; these are the rest.
///
/// ONE DEDICATED THREAD, not the shared pool: the pool can starve on the
/// 2-core VPS (see FlushPositions), and a queue of writes must not wait behind
/// the very work it is trying to get out of the way of.
///
/// NEVER AN OLD VALUE OVER A NEWER ONE. The queue holds WHO to save, never a
/// copy of what to save: each write reads the live values at the moment it
/// runs. Several requests for one player (or room) before the writer gets to
/// them collapse into one write of the latest values. And Habbo.SaveRpStats
/// reads and writes under a per-player lock, so a queued save and a direct one
/// (:hit, :heal, a medkit, a moderator command) cannot interleave: whichever
/// finishes last carries the newest values.
///
/// THE ORDERED QUEUE is the other kind: writes of what WAS, made in the order
/// they happened - a room visit opened and closed, the position a player left
/// a room at, the periodic position snapshots. A room change used to make
/// those writes itself, one after another, before the player was let into the
/// next room; now it only queues them. In order matters: a visit is closed
/// after it was opened, and a newer position can never be overwritten by an
/// older one (each position save used to be its own pool task, free to land
/// in any order).
///
/// A RESTART WAITS FOR IT (Drain, from PlusEnvironment's shutdown), so a
/// deploy loses nothing; only a crash can lose what was still waiting - in
/// practice the last few milliseconds.
/// </summary>
public static class RoomTickWriter
{
    private static readonly ConcurrentDictionary<int, Habbo> PendingStats = new();
    private static readonly ConcurrentDictionary<uint, Room> PendingCounts = new();
    private static readonly ConcurrentQueue<Action> Ordered = new();
    private static readonly ManualResetEventSlim Wake = new(false);
    private static readonly object StartLock = new();
    private static Thread? _thread;
    private static int _busy;

    /// <summary>Save this player's RP stats soon, with whatever they are then.</summary>
    public static void QueueRpStats(Habbo? habbo)
    {
        if (habbo == null)
            return;
        PendingStats[habbo.Id] = habbo;
        Signal();
    }

    /// <summary>Write this room's users_now soon, from its live count then.</summary>
    public static void QueueUserCount(Room? room)
    {
        if (room == null)
            return;
        PendingCounts[room.RoomId] = room;
        Signal();
    }

    /// <summary>Make this write soon, after every ordered write queued before it.</summary>
    public static void QueueOrdered(Action write)
    {
        Ordered.Enqueue(write);
        Signal();
    }

    /// <summary>A room visit begins: one row, open until its exit is written.</summary>
    public static void QueueVisitEntry(int userId, uint roomId)
    {
        var entryTimestamp = UnixTimestamp.GetNow();
        QueueOrdered(() =>
        {
            using var dbClient = PlusEnvironment.DatabaseManager.Connection();
            dbClient.Execute("INSERT INTO user_roomvisits (user_id,room_id,entry_timestamp,exit_timestamp) VALUES (@userId, @roomId, @entryTimestamp, 0)",
                new { userId, roomId, entryTimestamp });
        });
    }

    /// <summary>
    /// A room visit ends: close the newest OPEN visit to this room. (It used to
    /// close the row with the highest exit time - from a player's second visit
    /// to a room, an older, already closed one - so the visit in progress was
    /// left open for ever and the old one's exit was overwritten.)
    /// </summary>
    public static void QueueVisitExit(int userId, uint roomId)
    {
        var exitTimestamp = UnixTimestamp.GetNow();
        QueueOrdered(() =>
        {
            using var dbClient = PlusEnvironment.DatabaseManager.Connection();
            dbClient.Execute("UPDATE user_roomvisits SET exit_timestamp = @exitTimestamp WHERE user_id = @userId AND room_id = @roomId AND exit_timestamp = 0 ORDER BY id DESC LIMIT 1",
                new { userId, roomId, exitTimestamp });
        });
    }

    /// <summary>
    /// Wait for everything queued to be written - for the shutdown, so a
    /// restart loses nothing. False if it was still writing when the time ran
    /// out.
    /// </summary>
    public static bool Drain(TimeSpan timeout)
    {
        Signal();
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (PendingStats.IsEmpty && PendingCounts.IsEmpty && Ordered.IsEmpty && Volatile.Read(ref _busy) == 0)
                return true;
            Thread.Sleep(20);
        }
        return false;
    }

    private static void Signal()
    {
        if (_thread == null)
        {
            lock (StartLock)
            {
                if (_thread == null)
                {
                    var thread = new Thread(Loop) { IsBackground = true, Name = "PixelRPRoomWriter" };
                    thread.Start();
                    _thread = thread;
                }
            }
        }
        Wake.Set();
    }

    private static void Loop()
    {
        while (true)
        {
            try
            {
                Wake.Wait(1000);
                // Reset BEFORE draining: a request that lands after this point
                // sets Wake again, so nothing queued can be missed.
                Wake.Reset();
                // Busy BEFORE anything is taken off a queue, so Drain never
                // sees empty queues while a write is still in hand.
                Volatile.Write(ref _busy, 1);

                foreach (var id in PendingStats.Keys)
                {
                    if (PendingStats.TryRemove(id, out var habbo))
                        Try(() => SaveRpStats(habbo));
                }

                foreach (var id in PendingCounts.Keys)
                {
                    if (PendingCounts.TryRemove(id, out var room))
                        Try(() => SaveUserCount(room));
                }

                while (Ordered.TryDequeue(out var write))
                    Try(write);
            }
            catch (Exception e)
            {
                // Same rule as the movement threads: this loop must not end.
                ExceptionLogger.LogException(e);
                Thread.Sleep(100);
            }
            finally
            {
                Volatile.Write(ref _busy, 0);
            }
        }
    }

    // One failed write must not cost the others in the same pass.
    private static void Try(Action write)
    {
        try
        {
            write();
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    private static void SaveRpStats(Habbo habbo)
    {
        // A player who logged out and back in before this ran has a NEW Habbo,
        // loaded from the database. The old object's values must not land over
        // the new session's, so the write is skipped; the new session saves its
        // own. (A player who is simply offline has no newer object, and their
        // last values are still written.)
        var live = PlusEnvironment.Game?.ClientManager?.GetClientByUserId(habbo.Id)?.GetHabbo();
        if (live != null && !ReferenceEquals(live, habbo))
            return;

        habbo.SaveRpStats();
    }

    private static void SaveUserCount(Room room)
    {
        // Live count, read now: a room unloaded meanwhile has already set it to
        // 0 (RoomUserManager.Dispose), and this then writes 0 again, not the
        // count it had when the tick asked.
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `rooms` SET `users_now` = @count WHERE `id` = @id LIMIT 1");
        dbClient.AddParameter("count", room.UsersNow);
        dbClient.AddParameter("id", room.RoomId);
        dbClient.RunQuery();
    }
}
