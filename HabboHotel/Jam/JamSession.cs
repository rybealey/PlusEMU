using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Jam;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Jukebox;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Jam;

/// <summary>
/// pixelrp: a JAM - one person's music, playing for the people they invited.
///
/// The room jukebox and this are the same machine with different audiences. A
/// station's listeners are "whoever is standing in the room"; a jam's are
/// "whoever accepted an invite", and that is the whole of the difference. The
/// queue, the clock, the start time, the auto-advance and the duration reports
/// are all lifted straight from JukeboxStation, deliberately - two ideas of
/// what a shared timeline is would be two sets of bugs.
///
/// JukeboxTrack is reused rather than copied for the same reason. It already
/// carries QueuedBy and QueuedById, which is exactly what the queue list needs
/// to name who asked for each song.
///
/// A jam OUTRANKS a room's jukebox, everywhere, always. Walking into a room
/// with a jukebox does not interrupt it - the client yields the room's track to
/// a jam the same way it already yields to a song of your own, because as far
/// as the ears are concerned a jam IS a song of your own with company.
///
/// Nothing is persisted. A jam lives in memory and dies with the emulator,
/// which is the honest reading of a listening session: there is nothing to come
/// back to once everyone has gone.
/// </summary>
public class JamSession
{
    // Matches the room jukebox. Not because a jam has the same crowding problem
    // - it does not, you chose who is here - but because a queue long enough to
    // outlive everyone's attention is the same bad experience in both places.
    private const int MaxQueue = 20;
    // Per person. The room's one-song-each rule is NOT copied: there, it stops a
    // stranger monopolising a public queue. Here, everyone was invited and the
    // host can pull anything, so the only thing worth stopping is a paste-spam
    // burst - which a cooldown alone already stops.
    private const int AddCooldownSec = 30;
    private const int UnknownDurationCapSec = 600;
    // How long a disconnected member keeps their place. Refreshing the client
    // is a disconnect: without this, every reload would drop you out of the jam
    // you are listening to, and drop the HOST's jam onto somebody else.
    private const int AwayGraceSec = 90;
    // The queue's size, DERIVED rather than typed again. Both were 20, which
    // looked the same and was not: two constants that happen to agree today
    // quietly stop agreeing the first time one of them is changed, and a session
    // remembering more than it promises is a strange shape.
    private const int MaxHistory = MaxQueue;
    // Press the back button later than this into a song and you meant "play this
    // again", not "play the last one" - which is how every music player has
    // behaved for forty years, and the reason one button can do both jobs.
    private const int RestartWindowSec = 5;

    public int Id { get; }

    private readonly object _lock = new();
    // ORDERED, host first. "The jam passes to the next person" needs an answer
    // to which person that is, and join order is the only ordering anybody can
    // see from the outside - the longest-serving guest takes it.
    private readonly List<JamMember> _members = new();
    private readonly List<JukeboxTrack> _queue = new();
    // WHAT HAS ALREADY PLAYED, oldest first. Nothing kept this before: a track
    // ending overwrote the current one and the old one was dropped on the floor,
    // which is why no player in this hotel has ever had a back button.
    //
    // It belongs to the JAM, not to whoever is hosting, so it survives a
    // handover - the songs played are a fact about the session, not about the
    // person in charge of it.
    private readonly List<JukeboxTrack> _history = new();
    // WHO HAS BEEN PUT OUT. A kick that lets you walk back in through the
    // invite still sitting in your messages is not a kick - the card carries the
    // jam's id and Join would take it. Per jam, and it dies with the jam.
    private readonly HashSet<int> _kicked = new();
    private readonly Dictionary<int, DateTime> _lastAddByUser = new();
    private JukeboxTrack _current;
    private DateTime _currentStartedAt;
    private DateTime? _pausedAt;
    private DateTime _lastCycle = DateTime.MinValue;

    public JamSession(int id, Habbo host)
    {
        Id = id;
        _members.Add(new JamMember { Id = host.Id, Username = host.Username });
    }

    // Caller must hold _lock. A paused jam's clock stands still: elapsed is
    // measured to the moment it stopped rather than to now, or every pause
    // would silently fast-forward the track by however long it lasted.
    private int ElapsedSec => (_current == null)
        ? 0
        : (int)(((_pausedAt ?? DateTime.UtcNow) - _currentStartedAt).TotalSeconds);

    public bool IsEmpty { get { lock (_lock) return _members.Count == 0; } }

    public int HostId { get { lock (_lock) return (_members.Count == 0) ? 0 : _members[0].Id; } }

    public string HostName { get { lock (_lock) return (_members.Count == 0) ? "" : _members[0].Username; } }

    public bool IsHost(int userId) { lock (_lock) return (_members.Count > 0) && (_members[0].Id == userId); }

    public bool Has(int userId) { lock (_lock) return _members.Any(member => member.Id == userId); }

    public List<int> MemberIds { get { lock (_lock) return _members.Select(member => member.Id).ToList(); } }

    /// <summary>Adds a listener, or wakes one who had gone away. Idempotent.</summary>
    public void Join(Habbo habbo)
    {
        lock (_lock)
        {
            if (_kicked.Contains(habbo.Id))
                return;
            var existing = _members.FirstOrDefault(member => member.Id == habbo.Id);
            if (existing != null)
            {
                // Back from a refresh. Their place in the order is untouched,
                // which is what keeps a host who reloaded the host.
                existing.AwaySince = null;
                existing.Username = habbo.Username;
                return;
            }
            _members.Add(new JamMember { Id = habbo.Id, Username = habbo.Username });
        }
    }

    /// <summary>A deliberate leave - the button, not a dropped connection.</summary>
    public void Leave(int userId)
    {
        lock (_lock) _members.RemoveAll(member => member.Id == userId);
    }

    /// <summary>
    /// A connection went away. NOT a leave: reloading the client disconnects,
    /// and losing your jam every time you refresh would make it unusable. The
    /// place is held for AwayGraceSec and Cycle() reaps it after that.
    /// </summary>
    public void MarkAway(int userId)
    {
        lock (_lock)
        {
            var member = _members.FirstOrDefault(entry => entry.Id == userId);
            if (member != null)
                member.AwaySince = DateTime.UtcNow;
        }
    }

    // Pre-flight only; the packet handler resolves metadata then calls Enqueue.
    // Guests queue on the same terms as the host - that is the point of a jam.
    public string TryAdd(GameClient session, string url)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return "Not right now.";
        lock (_lock)
        {
            if (!_members.Any(member => member.Id == habbo.Id))
                return "You are not in this jam.";
            if (_queue.Count >= MaxQueue)
                return "The jam's queue is full.";
            if (JukeboxStation.ParseVideoId(url) == null)
                return "That doesn't look like a YouTube link.";
            if (_lastAddByUser.TryGetValue(habbo.Id, out var last) && (DateTime.UtcNow - last).TotalSeconds < AddCooldownSec)
                return "Hold on a moment before queueing another song.";
            _lastAddByUser[habbo.Id] = DateTime.UtcNow;
        }
        return null;
    }

    public void Enqueue(JukeboxTrack track)
    {
        bool startNext;
        lock (_lock)
        {
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

    /// <summary>
    /// Starts the jam ALREADY PLAYING, on the song its host was listening to.
    ///
    /// Without this, starting a jam while a song of your own is on created an
    /// empty one: your song is a purely local thing until this moment, so the
    /// server had nothing to tell anybody. The host carried on hearing it, saw a
    /// jam, invited people - and every guest arrived to silence, because as far
    /// as the jam was concerned nothing was playing. The next song they queued
    /// went through properly and everybody heard that, which made it look like
    /// joining worked and only the first song was cursed.
    ///
    /// The clock is wound BACK by however far in the song already is, so the jam
    /// joins the song rather than the song restarting for the host.
    /// </summary>
    public void SeedCurrent(JukeboxTrack track, int elapsedSec)
    {
        lock (_lock)
        {
            // Only ever into an empty jam. A race with a real queue must not
            // knock out whatever actually started.
            if (_current != null || track == null)
                return;
            _current = track;
            _currentStartedAt = DateTime.UtcNow.AddSeconds(-Math.Clamp(elapsedSec, 0, 7200));
            _pausedAt = null;
        }
        BroadcastState();
    }

    private void StartNext()
    {
        lock (_lock)
        {
            // Onto the history before it is overwritten. This is the whole of
            // what a back button needed: somewhere for a finished song to go
            // other than nowhere.
            if (_current != null)
            {
                _history.Add(_current);
                if (_history.Count > MaxHistory)
                    _history.RemoveAt(0);
            }
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
            // A new track always starts playing. Carrying a pause across a skip
            // would strand everyone on silence with no obvious cause.
            _pausedAt = null;
        }
        BroadcastState();
    }

    /// <summary>
    /// Anyone in the jam can pull their OWN request; the host can pull anything.
    /// The same rule the room's queue uses, with the host standing where room
    /// rights stand.
    /// </summary>
    public bool TryRemove(GameClient session, int index)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return false;
        JukeboxTrack removed;
        lock (_lock)
        {
            if (index < 0 || index >= _queue.Count)
                return false;
            var isHost = (_members.Count > 0) && (_members[0].Id == habbo.Id);
            if (!isHost && _queue[index].QueuedById != habbo.Id)
                return false;
            removed = _queue[index];
            _queue.RemoveAt(index);
        }
        BroadcastState();
        NotifyRequester(removed, habbo, "removed from the jam's queue");
        return true;
    }

    /// <summary>
    /// ANY member may skip. That is a deliberate asymmetry with pause, which is
    /// the host's alone: pausing everyone is a state nobody else can undo, while
    /// a skip moves the jam on to the thing it was going to play anyway.
    /// </summary>
    public bool TrySkip(GameClient session)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return false;
        JukeboxTrack skipped;
        lock (_lock)
        {
            if (_current == null || !_members.Any(member => member.Id == habbo.Id))
                return false;
            skipped = _current;
        }
        StartNext();
        NotifyRequester(skipped, habbo, "skipped");
        return true;
    }

    /// <summary>
    /// The back button, which is two buttons wearing one face.
    ///
    /// Past the first few seconds it RESTARTS the song, because somebody
    /// pressing back in the middle of a track almost always means "play that
    /// again". In those first few seconds - or when nothing has played yet - it
    /// steps back a track instead.
    ///
    /// Stepping back does not throw the current song away. It goes to the FRONT
    /// of the queue, so whoever asked for it still gets their turn, and it plays
    /// next rather than being quietly deleted by somebody else's button. That
    /// can push the queue one past its cap, which is allowed: the cap is there
    /// to stop people piling songs on, and this is a song coming back.
    ///
    /// Any member may, like skip. A guest with both can hold a jam on one song
    /// forever, which skip alone never allowed - worth knowing, and still the
    /// right call next to a pause only the host has, because neither of these
    /// stops the music.
    /// </summary>
    public bool TryBack(GameClient session)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return false;
        lock (_lock)
        {
            if (_current == null || !_members.Any(member => member.Id == habbo.Id))
                return false;
            if (ElapsedSec > RestartWindowSec || _history.Count == 0)
            {
                _currentStartedAt = DateTime.UtcNow;
            }
            else
            {
                var previous = _history[^1];
                _history.RemoveAt(_history.Count - 1);
                _queue.Insert(0, _current);
                _current = previous;
                _currentStartedAt = DateTime.UtcNow;
            }
            // Either way it plays. Coming back to a song and finding it paused
            // would be the button half-working.
            _pausedAt = null;
        }
        BroadcastState();
        return true;
    }

    /// <summary>
    /// The host puts somebody out.
    ///
    /// SILENTLY. Nobody is told - not the room, not the jam, not the person. The
    /// music simply stops for them, which they will work out, and there is no
    /// announcement for the others to react to. A kick said out loud is a scene;
    /// this is meant to be the quiet end of somebody's evening in here.
    ///
    /// They cannot come back with the invite still sitting in their messages,
    /// which is the only thing that makes it a kick rather than a nudge.
    /// </summary>
    public bool TryKick(GameClient session, int targetId)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return false;
        lock (_lock)
        {
            if (_members.Count == 0 || _members[0].Id != habbo.Id)
                return false;
            // Not yourself. Ending a jam you are hosting is the other button.
            if (targetId == habbo.Id)
                return false;
            if (_members.RemoveAll(member => member.Id == targetId) == 0)
                return false;
            _kicked.Add(targetId);
        }
        return true;
    }

    /// <summary>
    /// The host's pause, which is everyone's. A guest pressing pause pauses
    /// their own player and nobody else's - that never reaches here.
    /// </summary>
    public bool TrySetPaused(GameClient session, bool paused)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return false;
        lock (_lock)
        {
            if ((_members.Count == 0) || (_members[0].Id != habbo.Id))
                return false;
            if (_current == null)
                return false;
            if (paused)
            {
                if (_pausedAt != null)
                    return false;
                _pausedAt = DateTime.UtcNow;
            }
            else
            {
                if (_pausedAt == null)
                    return false;
                // The track's start slides forward by however long the silence
                // lasted, so elapsed picks up exactly where it stopped.
                _currentStartedAt += (DateTime.UtcNow - _pausedAt.Value);
                _pausedAt = null;
            }
        }
        BroadcastState();
        return true;
    }

    private static void NotifyRequester(JukeboxTrack track, Habbo actor, string what)
    {
        try
        {
            if (track == null || actor == null || track.QueuedById == actor.Id) return;
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(track.QueuedById);
            if (client?.GetHabbo() == null) return;
            var title = string.IsNullOrEmpty(track.Title) ? "Your song" : $"'{track.Title}'";
            client.SendWhisper($"{title} was {what} by {actor.Username}.");
        }
        catch (Exception) { /* a notice, never worth failing the action */ }
    }

    /// <summary>
    /// Duration and end-of-track come from the players themselves, exactly as
    /// the room's do - except that a jam has several players watching the same
    /// track, so the first credible report wins and the rest change nothing.
    /// </summary>
    public void Report(GameClient session, int durationSec, bool ended)
    {
        var broadcastDuration = false;
        var startNext = false;
        lock (_lock)
        {
            if (_current == null || session.GetHabbo() == null)
                return;
            if (!_members.Any(member => member.Id == session.GetHabbo().Id))
                return;
            if (!ended && _current.DurationSec == 0 && durationSec >= 10 && durationSec <= 7200)
            {
                _current.DurationSec = durationSec;
                broadcastDuration = true;
            }
            else if (ended && _pausedAt == null)
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

    /// <summary>
    /// Runs off the game loop. Two jobs: advance a track that has overrun, and
    /// reap members whose grace has expired - which is where the host handover
    /// actually happens, a minute and a half after they vanished rather than the
    /// instant their connection blinked.
    /// </summary>
    public List<int> Cycle()
    {
        var startNext = false;
        JamMember newHost = null;
        var dropped = new List<int>();
        lock (_lock)
        {
            if ((DateTime.UtcNow - _lastCycle).TotalMilliseconds < 1000)
                return dropped;
            _lastCycle = DateTime.UtcNow;
            var hostBefore = (_members.Count > 0) ? _members[0].Id : 0;
            foreach (var member in _members.ToList())
            {
                if (member.AwaySince == null || (DateTime.UtcNow - member.AwaySince.Value).TotalSeconds < AwayGraceSec)
                    continue;
                _members.Remove(member);
                dropped.Add(member.Id);
            }
            if (dropped.Count > 0 && _members.Count > 0 && _members[0].Id != hostBefore)
                newHost = _members[0];
            if (_current != null && _pausedAt == null)
            {
                var cap = (_current.DurationSec > 0) ? (_current.DurationSec + 2) : UnknownDurationCapSec;
                startNext = ElapsedSec > cap;
            }
        }
        if (newHost != null)
            AnnounceNewHost(newHost);
        if (startNext)
            StartNext();
        else if (dropped.Count > 0)
            BroadcastState();
        // Handed back so the manager can forget these players' claim on this
        // jam. Leaving them pointed at it would quietly readmit them the moment
        // they reconnected - at the BACK of the order, which is not where they
        // were - and would stop them ever starting a jam of their own.
        return dropped;
    }

    private void AnnounceNewHost(JamMember host)
    {
        foreach (var userId in MemberIds)
        {
            try
            {
                var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
                if (client?.GetHabbo() == null) continue;
                client.SendWhisper((userId == host.Id)
                    ? "The jam is yours now."
                    : $"{host.Username} is hosting the jam now.");
            }
            catch (Exception) { }
        }
    }

    // Snapshot under the lock; the composer serializes outside it.
    public IServerPacket BuildState(int forUserId)
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
            return new RpJamStateComposer(
                Id,
                (_members.Count > 0) ? _members[0].Id : 0,
                (_members.Count > 0) ? _members[0].Username : "",
                (_members.Count > 0) && (_members[0].Id == forUserId),
                _pausedAt != null,
                _members.Select(member => new JamMemberView(member.Id, member.Username, member.AwaySince != null)).ToList(),
                currentSnapshot,
                ElapsedSec,
                new List<JukeboxTrack>(_queue),
                _history.Count > 0);
        }
    }

    /// <summary>
    /// To the members, and nobody else. Each gets their own packet because one
    /// field in it - whether they are the host - differs per person.
    /// </summary>
    public void BroadcastState()
    {
        foreach (var userId in MemberIds)
        {
            try
            {
                var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
                if (client?.GetHabbo() == null) continue;
                client.Send(BuildState(userId));
            }
            catch (Exception) { /* one member's dead socket is not everyone's problem */ }
        }
    }
}

public class JamMember
{
    public int Id { get; set; }
    public string Username { get; set; }
    /// <summary>Set when their connection went; null while they are here.</summary>
    public DateTime? AwaySince { get; set; }
}

/// <summary>What the client is told about one member. Away is shown rather than
/// hidden: somebody mid-refresh is still in the jam, and silently removing them
/// from the list for ninety seconds would look like they left.</summary>
public record JamMemberView(int Id, string Username, bool Away);
