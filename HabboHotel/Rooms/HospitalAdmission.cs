using System.Collections.Concurrent;
using System.Drawing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: nobody bleeds out on the pavement forever.
///
/// A knocked-out player lies where they fell while a paramedic has the chance
/// to come for them. If nobody does, the hospital comes for them instead: they
/// are taken to the HMMC headquarters and laid on a medical bed, which starts
/// the ordinary course of treatment (MedicalBed) and gets them back on their
/// feet without anybody having to be online to help.
///
/// THE WINDOW IS A DEADLINE FOR THE MEDICS, NOT A PUNISHMENT FOR THE PATIENT.
/// Three minutes, two for VIP - long enough that a hospital with somebody on
/// shift gets the call and the roleplay happens, short enough that being
/// knocked out at 4am is not the end of the session.
///
/// Held in memory and keyed by player id, like the police registries and for
/// the same reason: being down is a moment, not a property of an account. A
/// player who logs out and back in while down starts the clock again, which is
/// the forgiving direction to be wrong in.
/// </summary>
public static class HospitalAdmission
{
    /// <summary>How long somebody lies there before the hospital collects them.</summary>
    public const int SecondsDown = 180;

    /// <summary>The VIP ward answers sooner.</summary>
    public const int SecondsDownVip = 120;

    /// <summary>Player id -> when they went down.</summary>
    private static readonly ConcurrentDictionary<int, DateTime> Downed = new();

    /// <summary>Somebody has just hit zero. Start their clock.</summary>
    public static void OnKnockout(RoomUser user)
    {
        if (user == null || user.IsBot)
            return;
        Downed[user.UserId] = DateTime.UtcNow;
    }

    /// <summary>
    /// Stop tracking somebody - they are back up, or gone. Safe to call for a
    /// player who was never tracked.
    /// </summary>
    public static void Forget(int habboId) => Downed.TryRemove(habboId, out _);

    /// <summary>
    /// One room cycle for one player. Free for everybody while nobody in the
    /// hotel is down: one emptiness check.
    /// </summary>
    public static void Tick(Room room, RoomUser user)
    {
        if (room == null || user == null || user.IsBot)
            return;
        var id = user.UserId;

        // Back on their feet by any route - a medkit, :restore, a bed. The
        // clock is only for somebody still on the floor.
        if (!user.RpKnockedOut)
        {
            if (!Downed.IsEmpty)
                Downed.TryRemove(id, out _);
            return;
        }

        // Down but untracked, so start the clock from here. OnKnockout catches
        // the normal case at the exact moment of the knockout; this catches
        // every other way somebody can be on the floor without one having just
        // happened - a relog, a room change after a restart, or being down
        // already when this shipped. Without it those players lie there
        // forever, which is the one outcome the feature exists to prevent.
        if (!Downed.TryGetValue(id, out var since))
        {
            Downed[id] = DateTime.UtcNow;
            return;
        }

        // A medic has them, or they are already on a bed. Somebody is dealing
        // with this, so the hospital does not need to. The clock keeps RUNNING
        // rather than being cleared: a medic who picks somebody up and then
        // abandons them has not solved anything, and the deadline should still
        // arrive.
        if (Chat.Commands.User.Police.PoliceState.IsBeingEscorted(id) || MedicalBed.Under(room, user) != null)
            return;

        var habbo = user.GetClient()?.GetHabbo();
        if (habbo == null)
            return;
        var limit = habbo.IsVip ? SecondsDownVip : SecondsDown;
        if ((DateTime.UtcNow - since).TotalSeconds < limit)
            return;

        Downed.TryRemove(id, out _);
        Admit(room, user, habbo);
    }

    /// <summary>
    /// Take one patient in. Silent and harmless when there is no hospital to
    /// take them to - an unconfigured HQ leaves them exactly where they were,
    /// which is what the hotel did before any of this existed.
    /// </summary>
    private static void Admit(Room room, RoomUser user, Habbo habbo)
    {
        var hqId = HeadquartersRoomId();
        if (hqId == 0)
            return;
        if (!PlusEnvironment.Game.RoomManager.TryLoadRoom(hqId, out var hq) || hq == null)
            return;

        var bed = PickBed(hq);
        habbo.Client?.SendNotification("You were found unconscious and taken to the hospital.");

        // Already in the ward: no room change, just put them on the bed.
        if (room.RoomId == hqId)
        {
            if (bed == null)
                return;
            room.GetGameMap().TeleportToItem(user, bed);
            room.GetRoomUserManager()?.UpdateUserStatus(user, false);
            user.UpdateNeeded = true;
            return;
        }

        // Elsewhere. The arrival tile is set BEFORE the forward, the way
        // :summon does it - the marker is read as the room is entered, which
        // begins the moment PrepareRoom is acted on. With no bed to name they
        // still go, and arrive at the door.
        if (bed != null)
            habbo.PendingRestore = new PendingRoomRestore(hqId, bed.GetX, bed.GetY, bed.Rotation);
        habbo.PrepareRoom(hqId, "");
    }

    /// <summary>
    /// A bed in the ward, preferring an empty one.
    ///
    /// AN OCCUPIED BED IS STILL A BED. Stacking two patients is allowed - tile
    /// overlap makes it legal and a full ward is no reason to leave somebody in
    /// the street - so this is a preference and never a refusal.
    /// </summary>
    private static Item? PickBed(Room hq)
    {
        var items = hq.GetRoomItemHandler()?.GetFloor;
        if (items == null)
            return null;
        var map = hq.GetGameMap();
        Item? taken = null;
        foreach (var item in items)
        {
            if (item?.Definition == null || item.Definition.InteractionType != InteractionType.MedicalBed)
                continue;
            if (map != null && map.MapGotUser(new Point(item.GetX, item.GetY)))
            {
                taken ??= item;
                continue;
            }
            return item;
        }
        return taken;
    }

    /// <summary>
    /// The HMMC's headquarters room, or 0.
    ///
    /// Read fresh, and only when an admission actually fires - which is rare -
    /// rather than cached or asked per tick. A hospital that moves house
    /// should not need a restart to be found.
    /// </summary>
    private static uint HeadquartersRoomId()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT r.`id` FROM `rooms` r " +
            "JOIN `rp_corporations` c ON c.`id` = r.`corporation_id` " +
            "WHERE c.`service_type` = 'medical' ORDER BY r.`id` LIMIT 1");
        var row = dbClient.GetRow();
        return row == null ? 0u : Convert.ToUInt32(row["id"]);
    }
}
