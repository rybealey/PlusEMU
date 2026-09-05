using System.Text.RegularExpressions;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Jukebox;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Jukebox;

/// <summary>
/// pixelrp: THE hotel's one radio station. The queue and the clock used to
/// live per room; the phone's Music app tunes in from anywhere, so they live
/// here now and every room jukebox (and every phone) plays the same track at
/// the same moment. State goes hotel-wide on every change, composed per
/// client with THAT client's room's has-jukebox flag (the room panel shows
/// only where a jukebox stands; the phone doesn't care).
/// RoomJukeboxManager is the per-room adapter over this.
/// </summary>
public static class JukeboxStation
{
    private const int MaxQueue = 20;
    private const int AddCooldownSec = 30;
    private const int UnknownDurationCapSec = 600;
    private const int StaffRank = 5;

    private static readonly object Lock = new();
    private static readonly List<JukeboxTrack> Queue = new();
    private static readonly Dictionary<int, DateTime> LastAddByUser = new();
    private static JukeboxTrack _current;
    private static DateTime _currentStartedAt;
    private static DateTime _lastCycle = DateTime.MinValue;

    // Caller must hold Lock.
    private static int ElapsedSec => (_current == null) ? 0 : (int)(DateTime.UtcNow - _currentStartedAt).TotalSeconds;

    public static bool IsPlaying { get { lock (Lock) return _current != null; } }

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

    // Pre-flight checks only; the packet handler fetches metadata then calls Enqueue.
    // Works from anywhere - a room jukebox or the phone. One pending request per
    // player (staff excepted) plus a short cooldown.
    public static string TryAdd(GameClient session, string url)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return "Not right now.";
        lock (Lock)
        {
            if (Queue.Count >= MaxQueue)
                return "The queue is full.";
            if (ParseVideoId(url) == null)
                return "That doesn't look like a YouTube link.";
            if (!IsStaff(session) && Queue.Any(track => track.QueuedById == habbo.Id))
                return "You already have a song in the queue - it'll play soon.";
            if (LastAddByUser.TryGetValue(habbo.Id, out var last) && (DateTime.UtcNow - last).TotalSeconds < AddCooldownSec)
                return "Hold on a moment before queueing another song.";
            LastAddByUser[habbo.Id] = DateTime.UtcNow;
        }
        return null;
    }

    public static void Enqueue(JukeboxTrack track)
    {
        bool startNext;
        lock (Lock)
        {
            if (Queue.Count >= MaxQueue)
                return;
            Queue.Add(track);
            startNext = _current == null;
        }
        if (startNext)
            StartNext();
        else
            BroadcastState();
    }

    private static void StartNext()
    {
        lock (Lock)
        {
            if (Queue.Count == 0)
            {
                _current = null;
            }
            else
            {
                _current = Queue[0];
                Queue.RemoveAt(0);
                _currentStartedAt = DateTime.UtcNow;
            }
        }
        BroadcastState();
    }

    // Rights: staff anywhere, or room rights in a room that has a jukebox.
    private static bool CanManage(GameClient session)
    {
        if (IsStaff(session)) return true;
        var room = session.GetHabbo()?.CurrentRoom;
        return room != null && room.GetJukeboxManager().HasJukebox() && (room.CheckRights(session, true) || room.CheckRights(session));
    }

    public static bool TryRemove(GameClient session, int index)
    {
        lock (Lock)
        {
            if (index < 0 || index >= Queue.Count)
                return false;
            if (!CanManage(session) && Queue[index].QueuedById != session.GetHabbo().Id)
                return false;
            Queue.RemoveAt(index);
        }
        BroadcastState();
        return true;
    }

    public static bool TrySkip(GameClient session)
    {
        lock (Lock)
        {
            if (_current == null || !CanManage(session))
                return false;
        }
        StartNext();
        return true;
    }

    // Clients report the player's real duration once loaded, and the ended signal.
    public static void Report(GameClient session, int durationSec, bool ended)
    {
        var broadcastDuration = false;
        var startNext = false;
        lock (Lock)
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
    public static void Cycle()
    {
        bool startNext;
        lock (Lock)
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
    public static IServerPacket BuildState(bool hasJukebox)
    {
        lock (Lock)
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
            return new RpJukeboxStateComposer(hasJukebox, currentSnapshot, ElapsedSec, new List<JukeboxTrack>(Queue));
        }
    }

    private static bool HasJukeboxFor(GameClient client)
    {
        var room = client.GetHabbo()?.CurrentRoom;
        return room != null && room.GetJukeboxManager().HasJukebox();
    }

    /// <summary>Hotel-wide: every online client, composed for its own room; then every loaded jukebox furni follows the play state.</summary>
    public static void BroadcastState()
    {
        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
        {
            if (client?.GetHabbo() == null) continue;
            client.Send(BuildState(HasJukeboxFor(client)));
        }
        foreach (var room in PlusEnvironment.Game.RoomManager.GetRooms().ToList())
        {
            try { room.GetJukeboxManager().SyncJukeboxItemState(); }
            catch { /* a room mid-unload is not worth a broadcast failure */ }
        }
    }
}
