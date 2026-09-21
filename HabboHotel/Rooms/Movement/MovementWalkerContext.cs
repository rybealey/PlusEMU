namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: the walker's own traversal permissions, resolved LIVE.
///
/// ONE PLACE, BECAUSE THE BUG WAS TWO. The route request built a real context
/// from the real user (MovementV2Bridge) while every per-beat site built a
/// fresh DEFAULT one, so AllowOverride and IsMounted silently reverted to false
/// the moment the walk left its first tile. A moderator with :override got an
/// A* route straight through a wall, walked one tile into it, and beat two
/// re-checked that tile under ordinary rules and stopped them. Planning and
/// per-beat validation now come from the same function, which makes them agree
/// by construction rather than by being remembered at four call sites.
///
/// RE-ASKED, NEVER CARRIED. Deliberately not stored on MovementState: a
/// permission held when the walk STARTED is not a permission held now.
/// Somebody who clocks off mid-walk must be stopped at the corp gate rather
/// than coasting through on a rota they have left, and the same goes for
/// :override being switched off or a horse being dismounted. The context is
/// therefore cheap to build and built fresh, every beat, for every walker.
///
/// I-5 SAFE. The scheduler thread may not touch the database, sockets,
/// callbacks or any blocking sink. This reads two bool fields off an in-memory
/// RoomUser and asks <see cref="MovementDuty"/>, which was already being
/// called from this thread every beat and only reads ShiftManager's in-memory
/// sessions. It is one dictionary lookup, and it replaces a lookup that was
/// happening anyway inside MovementDuty - so this is marginally LESS work per
/// beat than what it replaces, not more.
/// </summary>
internal static class MovementWalkerContext
{
    /// <summary>
    /// The context for a walker the engine knows only by virtual id, which is
    /// how every scheduler-side caller knows one.
    /// </summary>
    public static TraverseContext For(Room? room, int virtualId) =>
        For(room?.GetRoomUserManager()?.GetRoomUserByVirtualId(virtualId));

    /// <summary>
    /// The context for a walker already in hand.
    ///
    /// FAILS CLOSED on the two permissions and OPEN on duty, matching what
    /// each default means. A user who cannot be resolved gets no override and
    /// no mount, because a permission that cannot be established is not one -
    /// whereas <see cref="MovementDuty"/> answers true for a bot or a pet,
    /// which a duty rota does not apply to and which must keep moving.
    /// </summary>
    public static TraverseContext For(RoomUser? user) => new(
        allowOverride: user?.AllowOverride ?? false,
        isMounted: user?.RidingHorse ?? false,
        cornerPolicy: CornerPolicy.Off,
        onDuty: MovementDuty.IsOnDuty(user));
}
