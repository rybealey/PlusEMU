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
    /// TEMPORARY DIAGNOSTIC, on edge 0 only. The walker was standing on the
    /// SAME TILE as the walker whose phase it aligned to, at the instant the
    /// request was made - not at the instant it started, which is up to an
    /// interval later and which the client can work out for itself.
    ///
    /// A FLAG BIT RATHER THAN A FIELD, deliberately. flags is already an int on
    /// the wire, so this changes no packet length and the two halves stay
    /// compatible in both directions: an older client ignores the bit, and a
    /// newer client reading an unset bit simply gets false. A new field would
    /// have forced client and emulator to deploy in the same instant.
    ///
    /// Remove with StartDelayMs and the [MV2/JOIN-START] log.
    /// </summary>
    public const int JoinStackedAtRequest = 0x0080;

    /// <summary>
    /// TEMPORARY DIAGNOSTIC. This edge's geometry was restaged by
    /// :forceredirect rather than by a player's click.
    ///
    /// A FLAG BIT AND A BORROWED FIELD, for the reason the bit above gives: a
    /// new field on the record would force client and emulator to deploy in the
    /// same instant, and this is a diagnostic, not a feature. When the bit is
    /// set, StartDelayMs carries the margin the harness ACTUALLY achieved, in
    /// milliseconds before the boundary - the one number the client cannot work
    /// out for itself, because it never sees the moment the server decided.
    ///
    /// Safe to borrow that field here: StartDelayMs is only meaningful on edge
    /// 0, and a correction is never index 0 - the index it rewrites is e+1, and
    /// e is at least 0. The two diagnostics therefore cannot collide on one
    /// record.
    ///
    /// Remove with the whole :forceredirect harness.
    /// </summary>
    public const int ForcedRedirect = 0x0100;

    /// <summary>
    /// pixelrp escorts: face this edge the way bits 12-14 say, not by its own
    /// step. Set on every edge of an escorted shadow (StageShadow), whose facing
    /// is the CAPTOR's - on a bend the shadow slides diagonally to the tile
    /// beside its captor, and facing that slide put it a beat behind the
    /// captor's turn until the status packet corrected it.
    ///
    /// A flag plus three bits of the same int, for the reason ForcedRedirect
    /// gives: no packet length changes, an older client ignores it, and a newer
    /// client reading an unset bit faces the step as before.
    /// </summary>
    public const int FacingOverride = 0x0200;

    /// <summary>Where the 0-7 facing sits when <see cref="FacingOverride"/> is set.</summary>
    public const int FacingShift = 12;

    public static int WithFacing(int flags, byte facing) =>
        flags | FacingOverride | ((facing & 0x7) << FacingShift);

}
