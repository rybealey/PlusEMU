namespace Plus.HabboHotel.Users;

/// <summary>
/// pixelrp last-position restore: set once at login when the user is
/// forwarded to their last room; consumed (and cleared) by the first room
/// entry. Expires 30s after login so it can never leak into a later manual
/// entry if the forward is denied (locked/full/banned room).
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
