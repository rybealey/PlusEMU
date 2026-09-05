using System.Drawing;
using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp Movement V2 (A9): a PURE classifier answering one question -
/// "can stepping onto this tile trigger something that changes movement?"
///
/// It executes NOTHING. No callback, no wired dispatch, no item state change.
/// It only reads item definitions so the scheduler can decide, at PLAN time,
/// whether committing onto the tile must arm the movement barrier.
///
/// WHY THIS EXISTS EVEN THOUGH WIRED IS "NOT ENABLED": verified in the working
/// tree, the wired execution path is completely ungated -
///   Item.UserWalksOnFurni:1130  room.GetWired().TriggerEvent(TriggerWalkOnFurni, ...)
///   WiredComponent.TriggerEvent  no enabled/disabled guard at all
///   Room.cs:407                  GetWired().OnCycle() runs unconditionally
/// and 210 wf_* furni definitions exist in the SQL (31_FullFurniLibrary.sql),
/// including wf_trg_walks_on_furni and wf_act_teleport_to. They are merely not
/// in the catalog. So "wired is off" is a CONTENT property, not a safety
/// property, and any wired item already placed fires today. The barrier is
/// generic, so it covers wired for free without any wired redesign.
///
/// Movement-changing categories that are live REGARDLESS of wired:
///   Teleport / Hopper  -> IsTeleporting + PrepareRoom (room change mid-route)
///   Banzaitele         -> mid-route teleport
///   OneWayGate         -> forced move / UnlockWalking
///   Roller             -> displacement
///   Freeze tiles       -> CanWalk changes
/// </summary>
public static class TileEffects
{
    /// <summary>
    /// True when entering <paramref name="tile"/> may change movement, so the
    /// next commit must wait for Q2 to finish this tile's effects.
    ///
    /// Unflagged tiles cost nothing: no barrier, no added latency, and lookahead
    /// is NEVER truncated because a tile is flagged.
    /// </summary>
    public static bool IsMovementCritical(RoomMovement room, Point tile)
    {
        if (room.Closed)
            return false;
        var map = room.Room.GetGameMap();
        if (map == null)
            return false;

        var items = map.GetAllRoomItemForSquare(tile.X, tile.Y);
        if (items == null || items.Count == 0)
            return false;

        // Any item at all can raise a wired walk-on/off trigger, because
        // Item.UserWalksOnFurni fires TriggerWalkOnFurni for every item.
        // Only pay that cost when the room actually holds a movement trigger.
        var wiredCanFire = RoomHasMovementWiredTriggers(room);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item?.Definition == null)
                continue;
            if (wiredCanFire)
                return true;
            if (IsMovementCriticalInteraction(item.Definition.InteractionType))
                return true;
        }
        return false;
    }

    public static bool IsMovementCriticalInteraction(InteractionType type)
    {
        switch (type)
        {
            case InteractionType.Teleport:
            case InteractionType.Hopper:
            case InteractionType.Banzaitele:
            case InteractionType.OneWayGate:
            case InteractionType.Roller:
            case InteractionType.FreezeTile:
            case InteractionType.FreezeTileBlock:
            case InteractionType.Freezeexit:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether this room holds any wired box that reacts to walking. Conservative
    /// by design: when the room's wired component cannot be inspected we assume
    /// it can fire, because a false negative here means a mid-route teleport
    /// executing one edge late.
    /// </summary>
    private static bool RoomHasMovementWiredTriggers(RoomMovement room)
    {
        var wired = room.Room.GetWired();
        return wired != null && wired.HasMovementTriggers;
    }
}
