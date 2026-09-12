using System.Collections.Concurrent;

namespace Plus.HabboHotel.Users.Banking;

/// <summary>
/// pixelrp: who is currently standing at an ATM.
///
/// The ATM window is opened by the SERVER when a player uses the furni, so
/// the deposit and withdraw packets must be answerable only while that is
/// true. Without this gate the packets are a bank the player carries in their
/// pocket, and the furni becomes decoration again.
///
/// A session is pinned to the room the machine is in, so walking out ends it
/// even if the window is still on screen, and it lapses on its own so a
/// client that closed the window without telling anyone does not leave a
/// standing permission behind.
/// </summary>
public static class AtmSessions
{
    /// <summary>
    /// How long a machine stays usable after it was opened. Long enough to
    /// think about an amount, short enough that a forgotten window is not a
    /// way to bank from across the hotel later.
    /// </summary>
    private const int LifetimeSeconds = 300;

    private static readonly ConcurrentDictionary<int, (uint RoomId, int ExpiresAt)> Open = new();

    public static void Start(int userId, uint roomId) =>
        Open[userId] = (roomId, (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + LifetimeSeconds);

    public static void End(int userId) => Open.TryRemove(userId, out _);

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
        if (session.ExpiresAt < (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() || session.RoomId != habbo.CurrentRoom.Id)
        {
            Open.TryRemove(habbo.Id, out _);
            return false;
        }
        return true;
    }
}
