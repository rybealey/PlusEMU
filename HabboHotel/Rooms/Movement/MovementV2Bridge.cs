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
    /// True when V2 has this unit enrolled. Bots and pets included - there is
    /// no second engine for them to fall back to.
    /// </summary>
    public static bool Owns(RoomUser? user)
    {
        if (user == null)
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

    /// <summary>Enrol a unit. Humans, bots and pets alike.</summary>
    public static void OnUserEnter(Room room, RoomUser user)
    {
        if (room == null || user == null)
            return;

        var movement = MovementRegistry.Attach(room);
        if (movement == null)
            return;

        lock (movement.MovementLock)
        {
            if (movement.Closed)
                return;
            var state = MovementRegistry.GetOrCreateState(movement, user.VirtualId);
            // Only real players establish and hold the room's movement phase.
            state.IsRealUser = !user.IsBot && !user.IsPet;
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
    /// Route tiles this unit has left to walk, or -1 when it is not moving.
    ///
    /// Exists so a caller can re-target BEFORE the current walk ends. A V2
    /// redirect keeps the timeline and the phase and plans from the terminal of
    /// the elapsing edge, so a new leg costs no beat and the avatar flows
    /// straight into it. Waiting for the walk to finish instead would stand the
    /// avatar still until the next 500ms tick noticed - up to a full beat of
    /// dead time between legs.
    /// </summary>
    public static int RemainingRouteTiles(RoomUser? user)
    {
        if (user == null)
            return -1;
        if (!MovementRegistry.TryGet(user.RoomId, out var movement) || movement == null || movement.Closed)
            return -1;

        lock (movement.MovementLock)
        {
            if (!movement.States.TryGetValue(user.VirtualId, out var state))
                return -1;
            if (state.Mode != MovementMode.Moving)
                return -1;
            return state.Route.Length - state.Route.Cursor;
        }
    }

    /// <summary>
    /// Route a walk request to V2. Returns void: there is no fallback engine,
    /// so an unroutable click is simply a no-op.
    /// </summary>
    public static void RequestMove(RoomUser user, int targetX, int targetY)
    {
        if (!Owns(user))
            return;

        if (!MovementRegistry.TryGet(user.RoomId, out var movement) || movement == null || movement.Closed)
            return;

        var now = MovementScheduler.Instance.Clock.NowMs;
        var target = new Point(targetX, targetY);
        var ctx = new TraverseContext(
            allowOverride: user.AllowOverride,
            isMounted: user.RidingHorse,
            cornerPolicy: CornerPolicy.Off);

        lock (movement.MovementLock)
        {
            if (movement.Closed)
                return;
            if (!movement.States.TryGetValue(user.VirtualId, out var state))
                return;
            // pixelrp police escort: a shadowed suspect goes where their captor
            // goes and nowhere else. CanWalk only gates the client's own click
            // (MoveAvatarEvent); this closes every server-side path too.
            if (state.ShadowedBy != 0)
                return;

            // Keep V2's idea of where the avatar stands in step with anything
            // else that moved it (roller, teleport, room entry). A Pending
            // walker has not moved and its tile is already correct.
            if (state.Mode != MovementMode.Moving && state.Mode != MovementMode.Pending)
            {
                state.Tile = new Point(user.X, user.Y);
                state.TileZ = user.Z;
            }

            if (state.Mode == MovementMode.Moving)
                MovementController.Redirect(movement, state, target, ctx, now);
            else if (state.Mode == MovementMode.Pending)
                // Still waiting on the phase boundary: swap the route, keep the
                // timeline. Restarting here would re-run alignment and could
                // push the boundary out again on every click.
                MovementController.RepathPending(movement, state, target, ctx, now);
            else
                MovementController.StartWalk(movement, state, target, ctx, now);
        }

        // Latency path: wake the scheduler immediately rather than waiting for
        // its next due time.
        MovementScheduler.Instance.Signal(movement);
    }

    /// <summary>
    /// Stop a unit's walk where it stands and close it out on the wire.
    /// RoomUser.ClearMovement only clears V1's own fields, so on its own a
    /// "frozen" avatar would finish its route while flagged unable to walk.
    /// </summary>
    public static void Halt(RoomUser? user)
    {
        if (user == null)
            return;
        if (!MovementRegistry.TryGet(user.RoomId, out var movement) || movement == null || movement.Closed)
            return;
        lock (movement.MovementLock)
        {
            if (movement.Closed)
                return;
            if (!movement.States.TryGetValue(user.VirtualId, out var state))
                return;
            if (state.Mode != MovementMode.Moving && state.Mode != MovementMode.Pending)
                return;
            MovementController.StopWalk(movement, state);
        }
        MovementScheduler.Instance.Signal(movement);
    }

    // ---- police escort ----------------------------------------------------
    // The suspect is the captor's SHADOW: they stop being a walker and, for
    // every record the captor puts on the wire, the controller stages one for
    // them one tile in front (MovementController.StageShadow). Nothing here
    // pathfinds for the suspect and nothing ever will while the link stands -
    // RequestMove refuses them outright. Pair/Unpair/Turn run on command and
    // packet threads holding only MovementLock, like RequestMove.

    /// <summary>
    /// Make <paramref name="suspect"/> the shadow of <paramref name="captor"/>.
    /// Any walk of the suspect's own is closed out, then they are displaced to
    /// the tile in front of the captor - in front of where the captor is
    /// heading if they are mid-walk - facing the captor's way. False when
    /// either unit is unknown to V2 or already in a pair.
    /// </summary>
    public static bool Pair(Room? room, RoomUser? captor, RoomUser? suspect)
    {
        if (room == null || captor == null || suspect == null || captor == suspect)
            return false;
        if (!MovementRegistry.TryGet(room.RoomId, out var movement) || movement == null || movement.Closed)
            return false;

        var map = room.GetGameMap();
        var now = MovementScheduler.Instance.Clock.NowMs;
        lock (movement.MovementLock)
        {
            if (movement.Closed)
                return false;
            if (!movement.States.TryGetValue(captor.VirtualId, out var c) ||
                !movement.States.TryGetValue(suspect.VirtualId, out var s))
                return false;
            if (c.ShadowVirtualId != 0 || c.ShadowedBy != 0 || s.ShadowedBy != 0 || s.ShadowVirtualId != 0)
                return false;

            Point anchor;
            byte facing;
            if (c.Mode == MovementMode.Moving)
            {
                anchor = c.EdgeTo;
                facing = c.Facing;
            }
            else
            {
                // Standing (or Pending, which has not moved): server truth is
                // where the avatar is and which way it faces - Facing here is
                // only refreshed by walks and turns, RotBody by everything.
                if (c.Mode != MovementMode.Pending)
                {
                    c.Tile = new Point(captor.X, captor.Y);
                    c.TileZ = captor.Z;
                }
                anchor = c.Tile;
                facing = (byte)captor.RotBody;
                c.Facing = facing;
            }

            c.ShadowVirtualId = s.VirtualId;
            s.ShadowedBy = c.VirtualId;
            MovementController.StageDisplacement(movement, s, MovementController.FrontTile(map, anchor, facing), facing, map, now);
        }
        MovementScheduler.Instance.Signal(movement);
        return true;
    }

    /// <summary>
    /// Break a pair from whichever side is known. The shadow is closed out
    /// with a walk-end on the tile it was last heading to, so a suspect let go
    /// mid-walk stops there instead of keeping a walking posture forever.
    /// Safe with either user null or already gone.
    /// </summary>
    public static void Unpair(Room? room, RoomUser? captor, RoomUser? suspect)
    {
        if (room == null || (captor == null && suspect == null))
            return;
        if (!MovementRegistry.TryGet(room.RoomId, out var movement) || movement == null || movement.Closed)
            return;

        var map = room.GetGameMap();
        lock (movement.MovementLock)
        {
            if (movement.Closed)
                return;
            MovementState? c = null;
            MovementState? s = null;
            if (captor != null)
                movement.States.TryGetValue(captor.VirtualId, out c);
            if (suspect != null)
                movement.States.TryGetValue(suspect.VirtualId, out s);
            // Follow the link for whichever side the caller could not name.
            if (c == null && s != null && s.ShadowedBy != 0)
                movement.States.TryGetValue(s.ShadowedBy, out c);
            if (s == null && c != null && c.ShadowVirtualId != 0)
                movement.States.TryGetValue(c.ShadowVirtualId, out s);

            if (c != null)
                c.ShadowVirtualId = 0;
            if (s != null && s.ShadowedBy != 0)
            {
                s.ShadowedBy = 0;
                MovementController.StageShadowEnd(movement, s, map);
            }
        }
        MovementScheduler.Instance.Signal(movement);
    }

    /// <summary>
    /// The captor turned on the spot. Facing is otherwise only set by walks,
    /// so record it, and move the shadow round to the new front. Ignored while
    /// the captor is mid-walk - the edge owns their facing then, and the
    /// client does not send LookTo for a walking avatar anyway.
    /// </summary>
    public static void Turn(Room? room, RoomUser? captor, byte facing)
    {
        if (room == null || captor == null)
            return;
        if (!MovementRegistry.TryGet(room.RoomId, out var movement) || movement == null || movement.Closed)
            return;

        var map = room.GetGameMap();
        var now = MovementScheduler.Instance.Clock.NowMs;
        lock (movement.MovementLock)
        {
            if (movement.Closed)
                return;
            if (!movement.States.TryGetValue(captor.VirtualId, out var c))
                return;
            if (c.Mode == MovementMode.Moving)
                return;
            c.Facing = facing;
            if (c.ShadowVirtualId == 0)
                return;
            if (!movement.States.TryGetValue(c.ShadowVirtualId, out var s) || s.ShadowedBy != c.VirtualId)
            {
                c.ShadowVirtualId = 0;
                return;
            }
            if (c.Mode != MovementMode.Pending)
            {
                c.Tile = new Point(captor.X, captor.Y);
                c.TileZ = captor.Z;
            }
            MovementController.StageDisplacement(movement, s, MovementController.FrontTile(map, c.Tile, facing), facing, map, now);
        }
        MovementScheduler.Instance.Signal(movement);
    }
}
