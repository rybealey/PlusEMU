using Plus.Communication.Packets.Outgoing.Jam;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using System.Collections.Concurrent;

namespace Plus.HabboHotel.Jam;

/// <summary>
/// pixelrp: every jam in the hotel, and the one rule that spans them - a player
/// is in exactly zero or one.
///
/// Static rather than a DI service, matching ShiftManager and NotesUtility:
/// this is one dictionary and a tick, reached from packet handlers, from the
/// game loop and from Habbo.OnDisconnect, and threading a new interface through
/// the container to be reached from all three would be ceremony around a
/// ConcurrentDictionary.
///
/// _byUser is what makes the one-at-a-time rule and the refresh rule both work.
/// Membership is keyed by USER id, not by connection, so a client that reloads
/// comes back and finds itself already in the jam it left a second ago - see
/// JamSession.MarkAway for the other half of that.
/// </summary>
public static class JamManager
{
    private static readonly ConcurrentDictionary<int, JamSession> _byId = new();
    private static readonly ConcurrentDictionary<int, int> _byUser = new();
    private static int _nextId;

    /// <summary>
    /// The jam this player is in, or null. Membership is checked against the
    /// session itself, not just the index: a member whose grace ran out is gone
    /// from the jam a tick before the index catches up, and for that tick the
    /// index alone would say they were still in it.
    /// </summary>
    public static JamSession GetFor(int userId) =>
        (_byUser.TryGetValue(userId, out var jamId) && _byId.TryGetValue(jamId, out var jam) && jam.Has(userId))
            ? jam
            : null;

    public static JamSession Get(int jamId) => _byId.TryGetValue(jamId, out var jam) ? jam : null;

    /// <summary>
    /// Starts one, with the caller hosting. Returns the jam, or null if they are
    /// already in one - hosting while a guest somewhere else is the two-jams
    /// case, and the whole design leans on there being no such thing.
    /// </summary>
    public static JamSession Start(Habbo habbo)
    {
        if (habbo == null || GetFor(habbo.Id) != null)
            return null;
        var jam = new JamSession(Interlocked.Increment(ref _nextId), habbo);
        _byId[jam.Id] = jam;
        _byUser[habbo.Id] = jam.Id;
        return jam;
    }

    /// <summary>
    /// Joins an existing jam, leaving whatever they were in first. Accepting an
    /// invite while already listening somewhere is a normal thing to do, and
    /// refusing it would leave the player to work out which button frees them.
    /// </summary>
    public static JamSession Join(Habbo habbo, int jamId)
    {
        if (habbo == null || !_byId.TryGetValue(jamId, out var jam))
            return null;
        var existing = GetFor(habbo.Id);
        if (existing != null)
        {
            if (existing.Id == jamId)
            {
                // Already here - a refresh, or a second tap. Wake them rather
                // than making a fuss about it.
                jam.Join(habbo);
                return jam;
            }
            Leave(habbo);
        }
        jam.Join(habbo);
        _byUser[habbo.Id] = jam.Id;
        jam.BroadcastState();
        return jam;
    }

    /// <summary>
    /// A deliberate leave - the button, not a dropped connection.
    ///
    /// THE HOST HAS ONLY ONE ENDING. A host who chooses to go ends the jam for
    /// everybody; there is no version of it that carries on without them. It
    /// used to hand over instead, which gave the host two different exits to
    /// tell apart at the moment they had already decided to stop.
    ///
    /// Losing your CONNECTION is still not this. That holds your place for a
    /// minute and a half and then passes the jam on - a host whose browser
    /// crashed did not decide anything, and cutting four people off for it would
    /// be punishing them for somebody else's accident.
    /// </summary>
    public static void Leave(Habbo habbo)
    {
        if (habbo == null)
            return;
        var jam = GetFor(habbo.Id);
        if (jam == null)
            return;
        if (jam.IsHost(habbo.Id))
        {
            End(habbo);
            return;
        }
        jam.Leave(habbo.Id);
        _byUser.TryRemove(habbo.Id, out _);
        // Told first and unconditionally: they are out whether or not the jam
        // survives them, and BroadcastState below no longer reaches them.
        SendEmpty(habbo.Id);
        if (jam.IsEmpty)
        {
            _byId.TryRemove(jam.Id, out _);
            return;
        }
        jam.BroadcastState();
    }

    /// <summary>
    /// The host stops the jam for everybody.
    ///
    /// Deliberately not the same thing as the host leaving, which hands the jam
    /// on and lets it carry on without them. Both are reasonable things to want
    /// at the end of a session - one is "I am done", the other is "we are done" -
    /// so neither is made to stand in for the other.
    /// </summary>
    public static void End(Habbo habbo)
    {
        if (habbo == null)
            return;
        var jam = GetFor(habbo.Id);
        if (jam == null || !jam.IsHost(habbo.Id))
            return;
        var members = jam.MemberIds;
        // Out of the index FIRST. Anything arriving from a member while the
        // goodbyes are going out must find no jam rather than half of one.
        _byId.TryRemove(jam.Id, out _);
        foreach (var userId in members)
            _byUser.TryRemove(userId, out _);
        foreach (var userId in members)
        {
            try
            {
                var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
                if (client?.GetHabbo() == null) continue;
                client.Send(RpJamStateComposer.None());
                if (userId != habbo.Id)
                    client.SendWhisper($"{habbo.Username} ended the jam.");
            }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// The host puts somebody out, and nobody is told.
    ///
    /// The kicked player's client is sent an empty state, which is what actually
    /// stops their music - no whisper, no notification, nothing for the rest of
    /// the jam to see. The silence is the point: see JamSession.TryKick.
    /// </summary>
    public static void Kick(Habbo host, int targetId)
    {
        if (host == null)
            return;
        var jam = GetFor(host.Id);
        if (jam == null || !jam.TryKick(PlusEnvironment.Game.ClientManager.GetClientByUserId(host.Id), targetId))
            return;
        _byUser.TryRemove(targetId, out _);
        SendEmpty(targetId);
        jam.BroadcastState();
    }

    /// <summary>
    /// The connection went, which is not the same as leaving. Their place is
    /// held: refreshing the client disconnects, and a jam you lose by reloading
    /// the page is not a jam anybody can use. Cycle() reaps them if they really
    /// have gone.
    /// </summary>
    public static void OnDisconnect(int userId)
    {
        try { GetFor(userId)?.MarkAway(userId); }
        catch (Exception) { /* a disconnect path must never throw */ }
    }

    /// <summary>
    /// Sends the caller their jam state, or an explicit "no jam" so a client
    /// that has just reloaded can tell the difference between not being in one
    /// and not having been told yet.
    /// </summary>
    public static void SendState(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        var jam = GetFor(habbo.Id);
        if (jam == null)
        {
            session.Send(RpJamStateComposer.None());
            return;
        }
        // Reaching for state is proof they are here, so a member who was marked
        // away on disconnect is marked back before the snapshot is taken.
        jam.Join(habbo);
        session.Send(jam.BuildState(habbo.Id));
    }

    private static void SendEmpty(int userId)
    {
        try
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
            client?.Send(RpJamStateComposer.None());
        }
        catch (Exception) { }
    }

    /// <summary>
    /// Driven by the game loop. Each session self-gates to once a second, so
    /// calling this on every 5ms iteration costs a dictionary walk and a
    /// stopwatch read - which is what the room jukeboxes already do.
    /// </summary>
    public static void Cycle()
    {
        if (_byId.IsEmpty)
            return;
        foreach (var jam in _byId.Values.ToList())
        {
            try
            {
                foreach (var userId in jam.Cycle())
                {
                    _byUser.TryRemove(userId, out _);
                    SendEmpty(userId);
                }
                if (!jam.IsEmpty)
                    continue;
                // Everybody's grace ran out. Take the jam out and take any
                // remaining claim on it with them, or they can never start
                // another one.
                _byId.TryRemove(jam.Id, out _);
                foreach (var pair in _byUser.Where(entry => entry.Value == jam.Id).ToList())
                    _byUser.TryRemove(pair.Key, out _);
            }
            catch (Exception) { /* one bad jam must not stop the game loop */ }
        }
    }
}
