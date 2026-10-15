using Plus.Database;

namespace Plus.HabboHotel.Rooms.Chat.Logs;

public sealed class ChatlogManager : IChatlogManager
{
    private readonly IDatabase _database;
    private const int FlushOnCount = 10;

    private readonly List<ChatlogEntry> _chatlogs;
    private readonly ReaderWriterLockSlim _lock;

    public ChatlogManager(IDatabase database)
    {
        _database = database;
        _chatlogs = new();
        _lock = new(LockRecursionPolicy.NoRecursion);
    }

    public void StoreChatlog(ChatlogEntry entry)
    {
        _lock.EnterUpgradeableReadLock();
        // pixelrp: released in a finally here and in FlushAndSave. A save that
        // threw (a database pool timeout) used to leave the lock held for good,
        // and every chat message after it, hotel-wide, then waited on it forever.
        try
        {
            _chatlogs.Add(entry);
            OnChatlogStore();
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    private void OnChatlogStore()
    {
        if (_chatlogs.Count >= FlushOnCount)
            FlushAndSave();
    }

    public void FlushAndSave()
    {
        _lock.EnterWriteLock();
        try
        {
            if (_chatlogs.Count > 0)
            {
                using var dbClient = _database.GetQueryReactor();
                foreach (var entry in _chatlogs)
                {
                    dbClient.SetQuery("INSERT INTO chatlogs (`user_id`, `room_id`, `timestamp`, `message`) VALUES " + "(@uid, @rid, @time, @msg)");
                    dbClient.AddParameter("uid", entry.PlayerId);
                    dbClient.AddParameter("rid", entry.RoomId);
                    dbClient.AddParameter("time", entry.Timestamp);
                    dbClient.AddParameter("msg", entry.Message);
                    dbClient.RunQuery();
                }
            }
        }
        finally
        {
            // pixelrp: cleared even when the save failed part way. Kept, the
            // batch would be tried again with every chat message after it -
            // growing without end while the database is down, and writing the
            // lines that did get in a second time once it is back.
            _chatlogs.Clear();
            _lock.ExitWriteLock();
        }
    }
}