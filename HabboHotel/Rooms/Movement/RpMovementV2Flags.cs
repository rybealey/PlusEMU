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
    /// pixelrp police escort: this edge belongs to a suspect escorted IN FRONT
    /// of their captor (StageShadow), and field 6 - the otherwise unused
    /// timingGroupId - carries the captor's VirtualId
    /// (<see cref="MovementEdgeRecord.ShadowOfVirtualId"/>).
    ///
    /// The client faces a walking step by the step itself, and so does the
    /// server - except for this suspect, faced the way the CAPTOR faces. On a
    /// turn the suspect's step is a slide round the captor, so the client drew
    /// it facing the slide until the status turned it to the captor's way: a
    /// visible snap. Knowing the captor, the client faces each suspect step by
    /// the captor's step with the same index - which is where this server's
    /// facing comes from - so the status only confirms it.
    ///
    /// A flag bit plus a field already on the wire, for the reason
    /// <see cref="JoinStackedAtRequest"/> gives: no packet length changes, and
    /// a client without it ignores both. A bit rather than "field 6 is
    /// non-zero" because VirtualId 0 is a real unit. Medical escorts, where the
    /// patient trails, are deliberately NOT marked and face as they always did.
    /// </summary>
    public const int EscortShadow = 0x0200;

    /// <summary>
    /// pixelrp pepper spray: this edge is FACED BY THE SERVER, not by its own
    /// step, and field 6 - the timingGroupId <see cref="EscortShadow"/> also
    /// borrows - carries the facing, 0-7. A pepper-sprayed player stumbles
    /// back from the officer still facing them (MovementState.FacingOverride),
    /// so every step of that walk faces one fixed way that is not the way it
    /// moves; a client facing it by the step would turn them round and the
    /// status would turn them back, every step.
    ///
    /// The two never share a record: a stumbling player is nobody's escort
    /// shadow - the spray ends any escort first. A flag bit and a borrowed
    /// field for the reason <see cref="JoinStackedAtRequest"/> gives: no packet
    /// length changes, and a client without it ignores both and faces by the
    /// step, as before.
    /// </summary>
    public const int FixedFacing = 0x0400;

}
