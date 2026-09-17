namespace Plus.HabboHotel.Users;

/// <summary>
/// pixelrp: where to put somebody when they next enter a room, instead of the
/// door. Consumed (and cleared) by the first room entry, so it can only ever
/// act once.
///
/// Two callers, one meaning:
///   * login, forwarding the user to the room they logged out of, on the tile
///     they left;
///   * :summon, naming the summoner's tile in the summoner's room.
///
/// Expires after 30s so it can never leak into a later manual entry if the
/// forward is refused - a locked, full or banned room, or a summons the target
/// simply does not act on.
/// </summary>
public sealed class PendingRoomRestore
{
    public uint RoomId { get; }
    public int X { get; }
    public int Y { get; }
    public int Rot { get; }
    public DateTime SetAt { get; }

    public PendingRoomRestore(uint roomId, int x, int y, int rot)
    {
        RoomId = roomId;
        X = x;
        Y = y;
        Rot = rot;
        SetAt = DateTime.UtcNow;
    }

    public bool IsFresh => (DateTime.UtcNow - SetAt).TotalSeconds <= 30;
}
