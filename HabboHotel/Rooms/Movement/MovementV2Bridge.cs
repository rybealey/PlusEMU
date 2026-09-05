using System.Drawing;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2: the ONLY surface V1 code calls into.
///
/// V2 is always on - there is no runtime toggle. These methods are still the
/// single choke point, so every V1 call site has exactly one place to consult
/// about who owns a given avatar.
///
/// Ownership rule: a user is owned by V2 or by V1, never both. When
/// <see cref="Owns"/> is true, V1 must skip that user entirely (its tick
/// movement and its instant-first-step path both gate on it), because two
/// systems writing one avatar's position is precisely the V1 defect V2 exists
/// to remove.
/// </summary>
public static class MovementV2Bridge
{
    /// <summary>
    /// True when V2 owns this user's movement.
    ///
    /// Bots and pets stay on V1 for the first beta test: making them
    /// authoritative is a later phase, and keeping them on V1 shrinks the blast
    /// radius of the first flip.
    /// </summary>
    public static bool Owns(RoomUser? user)
    {
        if (user == null || user.IsBot || user.IsPet)
            return false;
        if (!MovementRegistry.TryGet(user.RoomId, out var movement) || movement == null || movement.Closed)
            return false;
        lock (movement.MovementLock)
            return movement.States.ContainsKey(user.VirtualId);
    }

    /// <summary>Attach a room to the scheduler.</summary>
    public static void OnRoomLoaded(Room room)
    {
        if (room == null)
            return;
        MovementRegistry.Attach(room);
    }

    /// <summary>Enrol a user. Bots and pets stay on V1.</summary>
    public static void OnUserEnter(Room room, RoomUser user)
    {
        if (room == null || user == null || user.IsBot || user.IsPet)
            return;

        var movement = MovementRegistry.Attach(room);
        if (movement == null)
            return;

        lock (movement.MovementLock)
        {
            if (movement.Closed)
                return;
            var state = MovementRegistry.GetOrCreateState(movement, user.VirtualId);
            state.Tile = new Point(user.X, user.Y);
            state.TileZ = user.Z;
            state.EdgeTo = state.Tile;
            state.EdgeToZ = state.TileZ;
            state.Target = state.Tile;
            state.Facing = (byte)user.RotBody;
            state.Mode = MovementMode.Standing;
        }
    }

    /// <summary>Un-enrol a user. Safe to call unconditionally.</summary>
    public static void OnUserLeave(Room room, RoomUser user)
    {
        if (room == null || user == null)
            return;
        if (!MovementRegistry.TryGet(room.RoomId, out var movement) || movement == null)
            return;
        lock (movement.MovementLock)
        {
            // Dequeue BEFORE removal so nothing can be staged for a unit that
            // is already gone.
            MovementRegistry.RemoveState(movement, user.VirtualId);
        }
    }

    /// <summary>
    /// Route a walk request to V2. Returns TRUE when V2 handled it, in which
    /// case the V1 path must not run.
    /// </summary>
    public static bool TryHandleMoveTo(RoomUser user, int targetX, int targetY)
    {
        if (!Owns(user))
            return false;

        if (!MovementRegistry.TryGet(user.RoomId, out var movement) || movement == null || movement.Closed)
            return false;

        var now = MovementScheduler.Instance.Clock.NowMs;
        var target = new Point(targetX, targetY);
        var ctx = new TraverseContext(
            allowOverride: user.AllowOverride,
            isMounted: user.RidingHorse,
            cornerPolicy: CornerPolicy.Off);

        lock (movement.MovementLock)
        {
            if (movement.Closed)
                return false;
            if (!movement.States.TryGetValue(user.VirtualId, out var state))
                return false;

            // Keep V2's idea of where the avatar stands in step with V1's, in
            // case anything else moved it (roller, teleport, room entry).
            if (state.Mode != MovementMode.Moving)
            {
                state.Tile = new Point(user.X, user.Y);
                state.TileZ = user.Z;
            }

            var handled = state.Mode == MovementMode.Moving
                ? MovementController.Redirect(movement, state, target, ctx, now)
                : MovementController.StartWalk(movement, state, target, ctx, now);

            if (!handled)
                return true; // V2 owns this user; an unroutable click is a no-op, not a fallback to V1
        }

        // Latency path: wake the scheduler immediately rather than waiting for
        // its next due time.
        MovementScheduler.Instance.Signal(movement);
        return true;
    }
}
