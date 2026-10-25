using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Users.Banking;

/// <summary>
/// pixelrp: who is currently standing at an ATM.
///
/// The ATM window is opened by the SERVER when a player uses the furni, so
/// the deposit and withdraw packets must be answerable only while that is
/// true. Without this gate the packets are a bank the player carries in their
/// pocket, and the furni becomes decoration again.
///
/// A session is pinned to the machine it was opened at AND the tile the player
/// stood on to open it. The machine ticks while anybody has it open
/// (<see cref="Tick"/>), and stepping off that tile ends the session and closes
/// the window, the way stepping off the deposit box closes that (Twist,
/// 2026-10-25). It also lapses on its own so a client that closed the window
/// without telling anyone does not leave a standing permission behind.
/// </summary>
public static class AtmSessions
{
    /// <summary>
    /// How long a machine stays usable after it was opened. Long enough to
    /// think about an amount, short enough that a forgotten window is not a
    /// way to bank from across the hotel later.
    /// </summary>
    private const int LifetimeSeconds = 300;

    private readonly record struct Session(uint RoomId, uint ItemId, int X, int Y, int ExpiresAt);

    private static readonly ConcurrentDictionary<int, Session> Open = new();

    private static int Now => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>Opened at <paramref name="itemId"/>, standing on (x, y).</summary>
    public static void Start(int userId, uint roomId, uint itemId, int x, int y) =>
        Open[userId] = new Session(roomId, itemId, x, y, Now + LifetimeSeconds);

    public static void End(int userId) => Open.TryRemove(userId, out _);

    /// <summary>
    /// The player clicked to walk (OwnWalk.Started): the window closes at the
    /// click. <see cref="Tick"/> cannot see it sooner than the step off the
    /// tile LANDS - RoomUser.X/Y stays the tile being left until then - which
    /// made the close trail the avatar by half a second or more (Twist,
    /// 2026-10-25). The tick stays, for being moved by somebody else.
    /// </summary>
    public static void WalkedAway(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || !Open.TryRemove(habbo.Id, out _))
            return;
        session!.Send(new RpAtmCloseComposer());
    }

    /// <summary>
    /// True only while the player is still in the room the machine is in and
    /// the session has not lapsed. Anything else is treated as "not at an
    /// ATM" and the lapsed entry is cleared on the way out.
    /// </summary>
    public static bool IsAtMachine(Habbo? habbo)
    {
        if (habbo == null || habbo.CurrentRoom == null)
            return false;
        if (!Open.TryGetValue(habbo.Id, out var session))
            return false;
        if (session.ExpiresAt < Now || session.RoomId != habbo.CurrentRoom.Id)
        {
            Open.TryRemove(habbo.Id, out _);
            return false;
        }
        return true;
    }

    /// <summary>
    /// One machine's tick (Item.ProcessUpdates, each room cycle while it is in
    /// use). Somebody no longer on the tile they opened it from - walked off,
    /// pushed, escorted, gone from the room - or whose session lapsed has the
    /// window closed. It asks for another tick while anybody is still using it,
    /// so an idle machine costs nothing.
    /// </summary>
    public static void Tick(Item item)
    {
        var room = item.GetRoom();
        var now = Now;
        var inUse = false;
        foreach (var (userId, session) in Open)
        {
            if (session.ItemId != item.Id)
                continue;
            var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(userId);
            if (user != null && session.RoomId == room!.Id && session.ExpiresAt >= now &&
                user.X == session.X && user.Y == session.Y)
            {
                inUse = true;
                continue;
            }
            Close(userId, session);
        }
        if (inUse)
            item.RequestUpdate(1, false);
    }

    /// <summary>The machine was picked up: everybody at it is sent away.</summary>
    public static void CloseAll(Item item)
    {
        foreach (var (userId, session) in Open)
            if (session.ItemId == item.Id)
                Close(userId, session);
    }

    /// <summary>
    /// Ends exactly this session and closes the window. A newer one the player
    /// opened at another machine in between is left alone.
    /// </summary>
    private static void Close(int userId, Session session)
    {
        if (!Open.TryRemove(new KeyValuePair<int, Session>(userId, session)))
            return;
        PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.Send(new RpAtmCloseComposer());
    }
}
