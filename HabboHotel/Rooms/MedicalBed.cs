using System.Drawing;
using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: the bed at the end of the ambulance ride.
///
/// A paramedic gets somebody to hospital (PoliceState / the drop-off pad); this
/// is what happens once they are there. Three behaviours in one piece of furni,
/// because separating them would let a room ship two thirds of a hospital:
///
///   HEALS    anyone resting on it refills to full - two minutes from empty,
///            one for VIP. Proportional, so somebody half hurt waits half as
///            long (RpRegen's rate is read off the maximum).
///
///   SHIELDS  nobody on it can be escorted, by medic or officer. Without this
///            a patient mid-treatment could simply be carried off, which makes
///            the bed scenery rather than somewhere a job finishes.
///
///   DISCHARGES once they are whole again they are moved to one of the furni
///            named in the behaviour's id list, so a ward empties itself
///            instead of filling up with people who are done.
///
/// The treatment is NOT started for somebody who lies down already healthy:
/// there is nothing to treat, so there is nothing to finish, and a discharge
/// that fired on arrival would teleport anyone who sat on a hospital bed.
/// </summary>
public static class MedicalBed
{
    /// <summary>Seconds to refill a whole bar. The bed is slower than a medkit
    /// on purpose - it is a place you have to stay, not something you carry.</summary>
    public const int SecondsToFull = 120;

    /// <summary>The VIP ward moves twice as fast.</summary>
    public const int SecondsToFullVip = 60;

    /// <summary>The medical bed under this unit, or null.</summary>
    public static Item? Under(Room room, RoomUser user)
    {
        if (room == null || user == null)
            return null;
        var items = room.GetGameMap()?.GetAllRoomItemForSquare(user.X, user.Y);
        if (items == null)
            return null;
        foreach (var item in items)
        {
            if (item?.Definition != null && item.Definition.InteractionType == InteractionType.MedicalBed)
                return item;
        }
        return null;
    }

    /// <summary>
    /// Is this player under a bed's protection? Asked by :escort before it
    /// picks anybody up, for either flavour - a suspect who has been taken to
    /// hospital is a patient first.
    /// </summary>
    public static bool Shields(Room room, RoomUser user) => Under(room, user) != null;

    /// <summary>
    /// One room cycle for one player. Cheap for everybody not on a bed: a
    /// single square lookup, the same one UpdateUserStatus already does.
    /// </summary>
    public static void Tick(Room room, RoomUser user)
    {
        if (user == null || user.IsBot)
            return;
        var habbo = user.GetClient()?.GetHabbo();
        if (habbo == null)
            return;

        var bed = Under(room, user);
        if (bed == null)
        {
            // Got up, or was moved. Only a regen THIS bed started is stopped:
            // a medkit swallowed on the way out is not the bed's to cancel.
            if (user.MedicalBedId != 0)
            {
                user.MedicalBedId = 0;
                habbo.RpHealthRegen.Stop();
            }
            return;
        }

        habbo.EnsureRpStatsLoaded();

        // Coming round, FIRST - before the discharge below can return early.
        // The regen loop moves the number but nothing there lifts a knockout,
        // so a patient healed past zero would otherwise lie frozen at full
        // health. Gradual healing clears this long before the bar fills, but
        // ordering it after the discharge would make that a coincidence rather
        // than a guarantee. Fires once; the guard is false afterwards.
        if (user.RpKnockedOut && habbo.RpHealth > 0)
            room.GetRoomUserManager()?.ApplyRpKnockout(user);

        // Treatment finished. Only for somebody this bed was actually treating -
        // MedicalBedId is set when a course starts, so anyone who lay down
        // already healthy is simply left to rest.
        if (user.MedicalBedId == bed.Id && habbo.RpHealth >= habbo.RpHealthMax)
        {
            Discharge(room, user, bed);
            return;
        }

        // Start a course, or pick one back up after fresh damage. An already
        // running regen is left alone - a medkit taken before lying down is
        // faster than the bed for anyone without VIP, and overriding it would
        // slow the player down for using one.
        if (habbo.RpHealth < habbo.RpHealthMax && !habbo.RpHealthRegen.Running)
        {
            user.MedicalBedId = bed.Id;
            habbo.RpHealthRegen.Start(habbo.RpHealthMax, habbo.IsVip ? SecondsToFullVip : SecondsToFull);
        }
    }

    /// <summary>
    /// Send a healed patient to one of the discharge points, and end the
    /// treatment. With none configured - or none of them still in the room -
    /// they simply stay put: a bed with no exit is a bed, not a broken one.
    /// </summary>
    private static void Discharge(Room room, RoomUser user, Item bed)
    {
        user.MedicalBedId = 0;
        var habbo = user.GetClient()?.GetHabbo();
        habbo?.RpHealthRegen.Stop();

        var exit = PickExit(room, bed);
        if (exit == null)
            return;

        room.GetGameMap().TeleportToItem(user, exit);
        // Re-derive the pose from the new square: the bed's lay is furni-owned
        // (IsLying stays false for it), so UpdateUserStatus strips it and
        // applies whatever the discharge point is - a seat, or nothing.
        room.GetRoomUserManager()?.UpdateUserStatus(user, false);
        user.UpdateNeeded = true;
    }

    /// <summary>
    /// Where to put them. The behaviour carries a list rather than one id so a
    /// ward can have several doors, and an unoccupied one is preferred so a
    /// busy hospital does not stack every discharge on one tile. Chosen at
    /// random among the equally good, which spreads people out without needing
    /// to remember who went where.
    /// </summary>
    private static Item? PickExit(Room room, Item bed)
    {
        var ids = bed.Definition?.VendingIds;
        if (ids == null || ids.Count == 0)
            return null;
        var handler = room.GetRoomItemHandler();
        var map = room.GetGameMap();
        if (handler == null)
            return null;

        List<Item>? free = null;
        List<Item>? taken = null;
        foreach (var id in ids)
        {
            if (id <= 0)
                continue;
            var item = handler.GetFloor.FirstOrDefault(x => x.Id == (uint)id);
            if (item == null)
                continue;
            var occupied = map != null && map.MapGotUser(new Point(item.GetX, item.GetY));
            if (occupied)
                (taken ??= new List<Item>()).Add(item);
            else
                (free ??= new List<Item>()).Add(item);
        }

        var pool = free ?? taken;
        if (pool == null || pool.Count == 0)
            return null;
        return pool[Random.Shared.Next(pool.Count)];
    }
}
