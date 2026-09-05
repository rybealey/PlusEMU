namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: the flags field of packet 4110.
///
/// Lives in the movement namespace rather than on the composer so the
/// scheduler can classify an edge without the movement engine depending on the
/// Communication layer. The composer just writes what it is handed.
///
/// Exactly one of <see cref="Edge"/>, <see cref="WalkEnd"/> or
/// <see cref="Displacement"/> is set on any packet.
/// </summary>
public static class RpMovementV2Flags
{
    /// <summary>A real movement edge.</summary>
    public const int Edge = 0x0001;

    /// <summary>Terminal marker: this walk session is over.</summary>
    public const int WalkEnd = 0x0002;

    /// <summary>
    /// Hard reset - teleport, roller, forced move. The client drops its queue
    /// and repositions immediately. This is what stops stale edges rendering
    /// through a teleport, which V1 could not express at all.
    /// </summary>
    public const int Displacement = 0x0004;

    /// <summary>Arrival happens at the end of this edge.</summary>
    public const int FinalEdge = 0x0020;

    /// <summary>
    /// This edge index was previously advertised with different geometry,
    /// because a redirect raised RouteRevision. Timing is unchanged - only the
    /// geometry of indexes STRICTLY AFTER the elapsing edge may be replaced.
    /// </summary>
    public const int Correction = 0x0040;
}
