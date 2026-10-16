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
/// A caller that places somebody the instant they will next enter a room -
/// :summon, jail, hospital, capture - passes a short ttlSeconds so a forward
/// the target never acts on cannot resurface much later. The login forward
/// passes 0 (no time limit): its room entry can be many seconds after the SSO
/// ticket on a cold client load, and the marker is already safe without a
/// timer - it only applies to its own RoomId and is consumed by the very first
/// room entry of any kind, so it can never leak into a later one.
/// </summary>
public sealed class PendingRoomRestore
{
    public uint RoomId { get; }
    public int X { get; }
    public int Y { get; }
    public int Rot { get; }
    public DateTime SetAt { get; }
    private readonly int _ttlSeconds;

    public PendingRoomRestore(uint roomId, int x, int y, int rot, int ttlSeconds = 30)
    {
        RoomId = roomId;
        X = x;
        Y = y;
        Rot = rot;
        SetAt = DateTime.UtcNow;
        _ttlSeconds = ttlSeconds;
    }

    /// <summary>A 0 (or negative) ttl never expires: consumed on first entry.</summary>
    public bool IsFresh => _ttlSeconds <= 0 || (DateTime.UtcNow - SetAt).TotalSeconds <= _ttlSeconds;
}
