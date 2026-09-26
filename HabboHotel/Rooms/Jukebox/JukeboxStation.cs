using System.Text.RegularExpressions;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Jukebox;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Jukebox;

/// <summary>
/// pixelrp: ONE ROOM's jukebox - its queue, its clock, its now playing.
///
/// It was a single hotel-wide station for a while, so the phone could tune in
/// from anywhere. Per room again: a song requested in the nightclub plays in
/// the nightclub, and a flat two doors down hears its own. What that costs the
/// phone - something to play when you are nowhere near a jukebox - the Spotify
/// app's "Just for you" covers instead, and covers better, because it is yours
/// rather than everybody's.
///
/// One instance per room, owned by RoomJukeboxManager, which is the only thing
/// that should reach it. State goes to that room and nowhere else.
///
/// Nothing is persisted. A room that unloads takes its queue with it and comes
/// back quiet, which is the honest reading of an empty room: there was nobody
/// left for it to play to.
/// </summary>
public class JukeboxStation
{
    private const int MaxQueue = 20;
    private const int AddCooldownSec = 30;
    private const int UnknownDurationCapSec = 600;
    private const int StaffRank = 5;

    private readonly Room _room;

    public JukeboxStation(Room room)
    {
        _room = room;
    }

    private readonly object _lock = new();
    private readonly List<JukeboxTrack> _queue = new();
    // Per ROOM, not per hotel: the wait is on queueing HERE again, so a player
    // can put a song on in the club and another in a flat without waiting.
    private readonly Dictionary<int, DateTime> _lastAddByUser = new();
    private JukeboxTrack _current;
    private DateTime _currentStartedAt;
    private DateTime _lastCycle = DateTime.MinValue;

    // Caller must hold _lock.
    private int ElapsedSec => (_current == null) ? 0 : (int)(DateTime.UtcNow - _currentStartedAt).TotalSeconds;

    public bool IsPlaying { get { lock (_lock) return _current != null; } }

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

    private static bool IsStaff(GameClient session) => session?.GetHabbo() != null && session.GetHabbo().Rank >= StaffRank;

    /// <summary>The phone's app shows skip/remove-anyone to staff only (room rights don't travel with the phone).</summary>
    public static bool IsStationStaff(GameClient session) => IsStaff(session);

    /// <summary>
    /// What a client is told when there is no station to listen to - no room, or
    /// a room with no jukebox in it. Nothing playing, nothing queued, no jukebox
    /// here, so the app shows quiet rather than the last room's queue.
    /// </summary>
    public static IServerPacket EmptyState() => new RpJukeboxStateComposer(false, null, 0, new List<JukeboxTrack>());

    // Pre-flight checks only; the packet handler fetches metadata then calls Enqueue.
    // Works from anywhere - a room jukebox or the phone. One pending request per
    // player (staff excepted) plus a short cooldown.
    public string TryAdd(GameClient session, string url)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return "Not right now.";
        lock (_lock)
        {
            if (_queue.Count >= MaxQueue)
                return "The queue is full.";
            if (ParseVideoId(url) == null)
                return "That doesn't look like a YouTube link.";
            if (!IsStaff(session) && _queue.Any(track => track.QueuedById == habbo.Id))
                return "You already have a song in the queue - it'll play soon.";
            if (_lastAddByUser.TryGetValue(habbo.Id, out var last) && (DateTime.UtcNow - last).TotalSeconds < AddCooldownSec)
                return "Hold on a moment before queueing another song.";
            _lastAddByUser[habbo.Id] = DateTime.UtcNow;
        }
        return null;
    }

    // False when the queue filled up between TryAdd's pre-flight and this
    // (the metadata fetch in between is awaited), so the caller only
    // announces a song that really joined the queue.
    public bool Enqueue(JukeboxTrack track)
    {
        bool startNext;
        lock (_lock)
        {
            if (_queue.Count >= MaxQueue)
                return false;
            _queue.Add(track);
            startNext = _current == null;
        }
        if (startNext)
            StartNext();
        else
            BroadcastState();
        return true;
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

    // Rights: staff anywhere, or room rights in a room that has a jukebox.
    private bool CanManage(GameClient session)
    {
        if (IsStaff(session)) return true;
        // THIS room's queue, so THIS room's rights - not whichever room the
        // player happens to be standing in. The has-jukebox check is gone with
        // it: a station only exists where a jukebox does.
        return _room != null && (_room.CheckRights(session, true) || _room.CheckRights(session));
    }

    public bool TryRemove(GameClient session, int index)
    {
        JukeboxTrack removed;
        lock (_lock)
        {
            if (index < 0 || index >= _queue.Count)
                return false;
            if (!CanManage(session) && _queue[index].QueuedById != session.GetHabbo().Id)
                return false;
            removed = _queue[index];
            _queue.RemoveAt(index);
        }
        BroadcastState();
        // the requester hears about it when someone else pulled their song
        NotifyRequester(removed, session, "removed from the queue");
        return true;
    }

    /// <summary>
    /// Reorder the queue. The same rights that let you remove anyone's song:
    /// moving one down the list is the same kind of act done more gently, so it
    /// would be strange for the gentler one to need less.
    ///
    /// Both ends are checked against the list as it is NOW - a drag is decided
    /// on the client against the list it was showing, and in a public room a
    /// song can be removed or start playing while a finger is down.
    /// </summary>
    public bool TryMove(GameClient session, int from, int to)
    {
        lock (_lock)
        {
            if (!CanManage(session))
                return false;
            if (from < 0 || from >= _queue.Count || to < 0 || to >= _queue.Count || from == to)
                return false;
            var track = _queue[from];
            _queue.RemoveAt(from);
            _queue.Insert(to, track);
        }
        BroadcastState();
        return true;
    }

    public bool TrySkip(GameClient session)
    {
        JukeboxTrack skipped;
        lock (_lock)
        {
            if (_current == null || !CanManage(session))
                return false;
            skipped = _current;
        }
        StartNext();
        NotifyRequester(skipped, session, "skipped");
        return true;
    }

    private static void NotifyRequester(JukeboxTrack track, GameClient actor, string what)
    {
        try
        {
            if (track == null || actor?.GetHabbo() == null || track.QueuedById == actor.GetHabbo().Id) return;
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(track.QueuedById);
            if (client?.GetHabbo() == null) return;
            var title = string.IsNullOrEmpty(track.Title) ? "Your song" : $"'{track.Title}'";
            client.SendWhisper($"{title} was {what} by staff.");
        }
        catch (Exception) { /* a notice, never worth failing the action */ }
    }

    // Clients report the player's real duration once loaded, and the ended signal.
    public void Report(GameClient session, int durationSec, bool ended)
    {
        var broadcastDuration = false;
        var startNext = false;
        lock (_lock)
        {
            if (_current == null || session.GetHabbo() == null)
                return;
            // Only the queuer's own player is trusted to set the duration.
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

    // Server-side auto-advance safety net. Every room cycle calls this; the
    // guard makes it run at most once a second for the whole hotel.
    public void Cycle()
    {
        bool startNext;
        lock (_lock)
        {
            if (_current == null)
                return;
            if ((DateTime.UtcNow - _lastCycle).TotalMilliseconds < 1000)
                return;
            _lastCycle = DateTime.UtcNow;
            var cap = (_current.DurationSec > 0) ? (_current.DurationSec + 2) : UnknownDurationCapSec;
            startNext = ElapsedSec > cap;
        }
        if (startNext)
            StartNext();
    }

    // Snapshot under the lock; the composer serializes outside it.
    public IServerPacket BuildState(bool hasJukebox)
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
            return new RpJukeboxStateComposer(hasJukebox, currentSnapshot, ElapsedSec, new List<JukeboxTrack>(_queue));
        }
    }

    /// <summary>
    /// This room, and nobody else. What this replaced walked every online client
    /// in the hotel and composed a packet each, on every state change, because
    /// one station was everybody's.
    /// </summary>
    public void BroadcastState()
    {
        try { _room?.GetJukeboxManager()?.BroadcastState(); }
        catch { /* a room mid-unload is not worth failing the action over */ }
    }
}
