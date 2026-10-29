using System.Collections.Concurrent;
using System.Diagnostics;
using Plus.Communication.Packets.Outgoing.Rooms.Popular;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: the :hotrooms window - the five busiest rooms, by the players in
/// them (Twist, 2026-10-29). View only. Everyone can open it; only staff are
/// sent the room ids, so a player's client never has them.
///
/// It stays up to date while it is open: the game loop calls <see cref="Cycle"/>,
/// which returns on a dictionary check while nobody has it open, looks at the
/// loaded rooms every few seconds otherwise, and sends only when the list has
/// changed. The window says when it is closed (RpHotRoomsCloseEvent); a
/// watcher who logged out is dropped on the next look.
///
/// The count is the navigator's (RoomData.UsersNow): players the room can
/// see, so no bots and no invisible staff. Hidden rooms (RoomAccess.Invisible)
/// are left out, as the navigator leaves them out.
/// </summary>
public static class HotRooms
{
    public const int Shown = 5;

    private const long CheckEveryMs = 3000;

    public readonly record struct Row(uint Id, string Name, int Users, int MaxUsers);

    private static readonly ConcurrentDictionary<int, GameClient> Watchers = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static long _nextCheck;
    private static List<Row> _lastSent = new();

    public static void Open(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (session == null || habbo == null)
            return;
        Watchers[habbo.Id] = session;
        session.Send(new RpHotRoomsComposer(true, Top(), IsStaff(habbo)));
    }

    public static void Close(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo != null)
            Watchers.TryRemove(habbo.Id, out _);
    }

    /// <summary>Driven by the game loop, every iteration.</summary>
    public static void Cycle()
    {
        if (Watchers.IsEmpty)
            return;
        var now = Clock.ElapsedMilliseconds;
        if (now < _nextCheck)
            return;
        _nextCheck = now + CheckEveryMs;
        try
        {
            var top = Top();
            if (top.SequenceEqual(_lastSent))
                return;
            _lastSent = top;
            RpHotRoomsComposer? forStaff = null;
            RpHotRoomsComposer? forPlayers = null;
            foreach (var (userId, client) in Watchers)
            {
                var habbo = client.GetHabbo();
                if (habbo == null || habbo.Id != userId)
                {
                    Watchers.TryRemove(userId, out _);
                    continue;
                }
                client.Send(IsStaff(habbo)
                    ? forStaff ??= new RpHotRoomsComposer(false, top, true)
                    : forPlayers ??= new RpHotRoomsComposer(false, top, false));
            }
        }
        catch (Exception) { /* the window must never stop the game loop */ }
    }

    private static List<Row> Top() => PlusEnvironment.Game.RoomManager.GetRooms()
        .Where(room => room.UsersNow > 0 && room.Access != RoomAccess.Invisible)
        .OrderByDescending(room => room.UsersNow)
        .ThenBy(room => room.Id)
        .Take(Shown)
        .Select(room => new Row(room.Id, room.Name, room.UsersNow, room.UsersMax))
        .ToList();

    private static bool IsStaff(Habbo habbo) => habbo.Rank >= CommandListLayout.StaffRank;
}
