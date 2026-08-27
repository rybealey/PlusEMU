using System.Text.RegularExpressions;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Jukebox;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Jukebox;

// PixelRP: per-room YouTube jukebox. The room owns the queue and the clock;
// clients only render. State is one broadcast packet (RpJukeboxStateComposer);
// timing travels as elapsed seconds so no client clock sync is needed.
public class RoomJukeboxManager
{
    private const string JukeboxItemName = "jukebox*1";
    private const int MaxQueue = 20;
    private const int AddCooldownSec = 30;
    private const int UnknownDurationCapSec = 600;

    private readonly Room _room;
    private readonly object _lock = new();
    private readonly List<JukeboxTrack> _queue = new();
    private readonly Dictionary<int, DateTime> _lastAddByUser = new();
    private JukeboxTrack _current;
    private DateTime _currentStartedAt;

    public RoomJukeboxManager(Room room)
    {
        _room = room;
    }

    // Caller must hold _lock.
    private int ElapsedSec => (_current == null) ? 0 : (int)(DateTime.UtcNow - _currentStartedAt).TotalSeconds;

    public bool HasJukebox() =>
        _room.GetRoomItemHandler().GetFloor.Any(item => item.Definition.ItemName == JukeboxItemName);

    // Accepts full watch URLs, youtu.be links, shorts links or a bare 11-char id.
    public static string ParseVideoId(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;
        input = input.Trim();
        if (Regex.IsMatch(input, "^[A-Za-z0-9_-]{11}$"))
            return input;
        var match = Regex.Match(input, @"(?:youtube\.com/(?:watch\?(?:.*&)?v=|shorts/|embed/)|youtu\.be/)([A-Za-z0-9_-]{11})");
        return match.Success ? match.Groups[1].Value : null;
    }

    // Pre-flight checks only; the packet handler fetches metadata then calls Enqueue.
    public string TryAdd(GameClient session, string url)
    {
        if (!HasJukebox())
            return "There's no jukebox in this room.";
        lock (_lock)
        {
            if (_queue.Count >= MaxQueue)
                return "The queue is full.";
            if (ParseVideoId(url) == null)
                return "That doesn't look like a YouTube link.";
            if (_lastAddByUser.TryGetValue(session.GetHabbo().Id, out var last) &&
                (DateTime.UtcNow - last).TotalSeconds < AddCooldownSec)
                return "Hold on a moment before queueing another song.";
            _lastAddByUser[session.GetHabbo().Id] = DateTime.UtcNow;
        }
        return null;
    }

    public void Enqueue(JukeboxTrack track)
    {
        bool startNext;
        lock (_lock)
        {
            // Closes the precheck/async-fetch race: TryAdd's queue-full check
            // ran before metadata resolution; re-check here under the lock
            // right before the actual mutation.
            if (_queue.Count >= MaxQueue)
                return;
            _queue.Add(track);
            startNext = _current == null;
        }
        if (startNext)
            StartNext();
        else
            BroadcastState();
    }

    private void StartNext()
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                _current = null;
            }
            else
            {
                _current = _queue[0];
                _queue.RemoveAt(0);
                _currentStartedAt = DateTime.UtcNow;
            }
        }
        BroadcastState();
    }

    public bool TryRemove(GameClient session, int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _queue.Count)
                return false;
            var canManage = _room.CheckRights(session, true) || _room.CheckRights(session);
            if (!canManage && _queue[index].QueuedById != session.GetHabbo().Id)
                return false;
            _queue.RemoveAt(index);
        }
        BroadcastState();
        return true;
    }

    public bool TrySkip(GameClient session)
    {
        lock (_lock)
        {
            if (_current == null || !(_room.CheckRights(session, true) || _room.CheckRights(session)))
                return false;
        }
        StartNext();
        return true;
    }

    // Clients report the player's real duration once loaded, and the ended signal.
    public void Report(GameClient session, int durationSec, bool ended)
    {
        var broadcastDuration = false;
        var startNext = false;
        lock (_lock)
        {
            if (_current == null)
                return;
            // Only the queuer's own player has actually loaded this track, so
            // only their client's report can be trusted to set the duration —
            // otherwise anyone in the room could inject a bogus (10, false)
            // to truncate every song or a (7200, false) to freeze the room.
            // If the queuer leaves before reporting, the 600s Cycle cap
            // still backstops an unset duration.
            if (!ended && _current.DurationSec == 0 && durationSec >= 10 && durationSec <= 7200 &&
                session.GetHabbo().Id == _current.QueuedById)
            {
                _current.DurationSec = durationSec;
                broadcastDuration = true;
            }
            else if (ended)
            {
                var minElapsed = (_current.DurationSec > 0) ? (int)(_current.DurationSec * 0.8) : 30;
                startNext = ElapsedSec >= minElapsed;
            }
        }
        if (startNext)
            StartNext();
        else if (broadcastDuration)
            BroadcastState();
    }

    // Called from the room cycle: server-side auto-advance safety net.
    public void Cycle()
    {
        bool startNext;
        lock (_lock)
        {
            if (_current == null)
                return;
            var cap = (_current.DurationSec > 0) ? (_current.DurationSec + 2) : UnknownDurationCapSec;
            startNext = ElapsedSec > cap;
        }
        if (startNext)
            StartNext();
    }

    public void OnJukeboxPlaced() => BroadcastState();

    public void OnJukeboxRemoved()
    {
        if (HasJukebox())
            return;
        lock (_lock)
        {
            _current = null;
            _queue.Clear();
        }
        BroadcastState();
    }

    // Snapshots _current/_queue under the lock so the composer (serialized
    // outside the lock, on whichever thread calls Send) never reads state
    // that another thread is concurrently mutating.
    public IServerPacket BuildState()
    {
        lock (_lock)
        {
            var currentSnapshot = (_current == null)
                ? null
                : new JukeboxTrack
                {
                    VideoId = _current.VideoId,
                    Title = _current.Title,
                    Author = _current.Author,
                    DurationSec = _current.DurationSec,
                    QueuedBy = _current.QueuedBy,
                    QueuedById = _current.QueuedById
                };
            var queueSnapshot = new List<JukeboxTrack>(_queue);
            return new RpJukeboxStateComposer(HasJukebox(), currentSnapshot, ElapsedSec, queueSnapshot);
        }
    }

    // BuildState() takes and releases _lock internally, so the actual Send
    // below always runs outside the lock (no network I/O while holding it).
    public void BroadcastState() => _room.SendPacket(BuildState());

    public void SendState(GameClient session) => session.Send(BuildState());
}
