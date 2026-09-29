namespace Plus.HabboHotel.Rooms;

public static class RoomLimits
{
    /// <summary>
    ///     pixelrp: how many visitors a room may be set to hold. The Room tool and the room creator
    ///     offer 10 to 200 in tens (client RoomSettingsUtils.GetMaxVisitorsList); the server clamps to
    ///     the same range. Every room was set to the maximum once (SQL update 228), and it is the
    ///     default for a new room.
    /// </summary>
    public const int MinVisitors = 10;

    public const int MaxVisitors = 200;

    public const int DefaultVisitors = MaxVisitors;
}
