using System.Collections.Concurrent;
using System.Text;
using Plus.Database;

namespace Plus.HabboHotel.Rooms.Chat.Logs;

public sealed class ChatlogManager : IChatlogManager
{
    private readonly IDatabase _database;
    private const int FlushOnCount = 10;
    // pixelrp: rows per INSERT, so even a long backlog is a handful of
    // statements rather than one per line.
    private const int RowsPerInsert = 100;

    // pixelrp: chat only ever adds to this queue. The save used to run on the
    // chatting player's thread, as ten INSERTs one after another, while a lock
    // held every other chat line in the hotel - so a slow database lagged
    // everybody's chat and commands. It now runs on RoomTickWriter's thread,
    // as one statement.
    private readonly ConcurrentQueue<ChatlogEntry> _pending = new();
    // One save at a time: the moderator chatlog's own save (FlushAndSave, then
    // its SELECT) waits for a background save still writing, so it sees every
    // line stored before it asked.
    private readonly object _saveLock = new();
    private int _saveQueued;

    public ChatlogManager(IDatabase database)
    {
        _database = database;
    }

    public void StoreChatlog(ChatlogEntry entry)
    {
        _pending.Enqueue(entry);
        if (_pending.Count >= FlushOnCount && Interlocked.CompareExchange(ref _saveQueued, 1, 0) == 0)
        {
            RoomTickWriter.QueueOrdered(() =>
            {
                // Cleared before saving: a line stored from here on is either
                // in this save or queues the next one - none is left behind.
                Volatile.Write(ref _saveQueued, 0);
                FlushAndSave();
            });
        }
    }

    /// <summary>
    /// Write every line still waiting, now. The moderator chatlog calls this
    /// before it reads, and so does the shutdown.
    /// </summary>
    public void FlushAndSave()
    {
        lock (_saveLock)
        {
            var batch = new List<ChatlogEntry>();
            while (_pending.TryDequeue(out var entry))
                batch.Add(entry);
            if (batch.Count == 0)
                return;

            // pixelrp: a failed write is logged by the query adapter and its
            // lines are dropped, as before. Kept, they would be tried again with
            // every line after them while the database is down, and written a
            // second time once it is back.
            using var dbClient = _database.GetQueryReactor();
            for (var start = 0; start < batch.Count; start += RowsPerInsert)
            {
                var count = Math.Min(RowsPerInsert, batch.Count - start);
                var sql = new StringBuilder("INSERT INTO chatlogs (`user_id`, `room_id`, `timestamp`, `message`) VALUES ");
                for (var i = 0; i < count; i++)
                    sql.Append(i == 0 ? "" : ", ").Append($"(@uid{i}, @rid{i}, @time{i}, @msg{i})");
                dbClient.SetQuery(sql.ToString());
                for (var i = 0; i < count; i++)
                {
                    var entry = batch[start + i];
                    dbClient.AddParameter($"uid{i}", entry.PlayerId);
                    dbClient.AddParameter($"rid{i}", entry.RoomId);
                    dbClient.AddParameter($"time{i}", entry.Timestamp);
                    dbClient.AddParameter($"msg{i}", entry.Message);
                }
                dbClient.RunQuery();
            }
        }
    }
}