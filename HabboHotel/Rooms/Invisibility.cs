using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: :invisible (2026-10-22). Trial Moderator and up go unseen
/// while they move through rooms: nobody sees their avatar, chat, typing or
/// enable except others who are invisible too, and they still see and hear the
/// whole room. A moderator who is invisible can bring anyone in the room into
/// it with :invisible &lt;name&gt;, to talk with them out of everyone's sight.
///
/// The rule is a packet about an invisible unit never reaches a viewer who
/// can't see it. The client draws a nameless bubble for chat from an index it
/// doesn't know, and anyone reading their packets would see the unit is there.
/// Room.SendPacket and Room.SendPacketsBatched hold such packets back
/// (IUnitPacket); this class only handles the moment someone turns
/// invisible or visible, which is when viewers gain or lose the avatar.
///
/// It lives on the session (Habbo.IsInvisible), so it follows them from room
/// to room and ends at logout. A player who isn't staff can't turn it off
/// themselves, so when they were brought in it ends with the one who brought
/// them: when that moderator turns visible or leaves the room, or when they
/// leave it themselves. Staff who were brought in stay until they turn it off.
/// </summary>
public static class Invisibility
{
    /// <summary>Makes <paramref name="target"/> invisible.
    /// <paramref name="bringer"/> is the moderator who did it, or null when
    /// they turned it on themselves.</summary>
    public static void Enable(Room room, RoomUser target, RoomUser bringer = null)
    {
        if (target?.GetClient()?.GetHabbo() is not { } habbo || habbo.IsInvisible)
            return;
        var users = room.GetRoomUserManager().GetUserList().ToList();
        habbo.IsInvisible = true;
        habbo.InvisibleBroughtById = bringer != null && bringer != target ? bringer.HabboId : 0;
        var appeared = new List<RoomUser>();
        var employments = CorporationUtility.GetEmployments(users.Where(user => user is { IsInvisible: true } && user != target).Select(user => user.HabboId))
            .GroupBy(employment => employment.UserId)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var viewer in users)
        {
            if (viewer == null || viewer == target || viewer.IsBot || viewer.GetClient() == null)
                continue;
            if (viewer.IsInvisible)
            {
                // Everyone already invisible appears to them.
                employments.TryGetValue(viewer.HabboId, out var employment);
                room.SendUnitTo(target.GetClient(), viewer, employment);
                appeared.Add(viewer);
            }
            else
            {
                viewer.GetClient().Send(new UserRemoveComposer(target.VirtualId));
            }
        }
        if (appeared.Count > 0)
            target.GetClient().Send(new UserUpdateComposer(appeared));
        // Broadcast after the flag is set, so only those who can see them get it.
        habbo.Effects?.ApplyEffect(Habbo.InvisibleEffectId);
    }

    /// <summary>Makes <paramref name="target"/> visible again, and anyone they
    /// brought in who isn't staff along with them.</summary>
    public static void Disable(Room room, RoomUser target)
    {
        if (target?.GetClient()?.GetHabbo() is not { } habbo || !habbo.IsInvisible)
            return;
        habbo.IsInvisible = false;
        habbo.InvisibleBroughtById = 0;
        // Ahead of the avatar going out, so the enable it is sent with is the
        // real one. Those who can't see them yet drop this for an avatar they
        // don't have.
        if (habbo.Effects != null && habbo.Effects.CurrentEffect == Habbo.InvisibleEffectId)
            habbo.Effects.ApplyEffect(RestoreEffectId(habbo));
        var users = room.GetRoomUserManager().GetUserList().ToList();
        var employment = CorporationUtility.GetEmployment(habbo.Id);
        var status = new UserUpdateComposer(new List<RoomUser> { target });
        foreach (var viewer in users)
        {
            if (viewer == null || viewer == target || viewer.IsBot || viewer.GetClient() == null)
                continue;
            if (viewer.IsInvisible)
            {
                // Still invisible: they go out of sight.
                target.GetClient().Send(new UserRemoveComposer(viewer.VirtualId));
            }
            else
            {
                room.SendUnitTo(viewer.GetClient(), target, employment);
                viewer.GetClient().Send(status);
            }
        }
        foreach (var brought in BroughtInBy(users, habbo.Id))
        {
            Disable(room, brought);
            brought.GetClient()?.SendWhisper($"{habbo.Username} is visible again, and so are you.");
        }
    }

    /// <summary>From RoomUserManager.RemoveRoomUser, once <paramref name="user"/>
    /// is out of the room: whoever they brought in who isn't staff turns
    /// visible, and so do they if that is how they came to be invisible.</summary>
    public static void OnLeaveRoom(Room room, RoomUser user)
    {
        if (user == null || user.IsBot)
            return;
        foreach (var brought in BroughtInBy(room.GetRoomUserManager().GetUserList().ToList(), user.HabboId))
        {
            Disable(room, brought);
            brought.GetClient()?.SendWhisper("The moderator who made you invisible has left, so you're visible again.");
        }
        // Gone from the room already, so there is nobody left to show them to.
        if (user.GetClient()?.GetHabbo() is { IsInvisible: true, InvisibleBroughtById: > 0 } habbo && !IsStaff(habbo))
            Release(habbo);
    }

    /// <summary>From RoomUserManager.AddAvatarToRoom, before the room is told
    /// <paramref name="user"/> has arrived: a player brought in who isn't staff
    /// and comes in without the one who brought them is visible again. Every
    /// leave turns them visible (OnLeaveRoom), but a room unloaded around them
    /// has no leave.</summary>
    public static void OnEnterRoom(Room room, RoomUser user)
    {
        if (user?.GetClient()?.GetHabbo() is not { IsInvisible: true, InvisibleBroughtById: > 0 } habbo || IsStaff(habbo))
            return;
        if (room.GetRoomUserManager().GetRoomUserByHabbo(habbo.InvisibleBroughtById) is not { IsInvisible: true })
            Release(habbo);
    }

    /// <summary>Visible again while out of any room - nobody to show them to.
    /// The Ghost comes off in place, as ApplyEffect needs a room; the room
    /// they walk into next is told what they really wear.</summary>
    private static void Release(Habbo habbo)
    {
        habbo.IsInvisible = false;
        habbo.InvisibleBroughtById = 0;
        if (habbo.Effects != null && habbo.Effects.CurrentEffect == Habbo.InvisibleEffectId)
            habbo.Effects.CurrentEffect = RestoreEffectId(habbo);
    }

    /// <summary>The enable to wear once the Ghost comes off: City Government's
    /// while clocked in, the passive one while a smoothie runs, else none.</summary>
    public static int RestoreEffectId(Habbo habbo)
    {
        if (ShiftManager.IsStaffOnDuty(habbo.Id))
            return Habbo.StaffDutyEffectId;
        return habbo.RpPassiveSeconds > 0 ? Habbo.PassiveEnableEffectId : 0;
    }

    /// <summary>Staff - Trial Moderator and up - can turn it off themselves.</summary>
    public static bool IsStaff(Habbo habbo) => habbo.Permissions.HasRight("mod_tool");

    private static List<RoomUser> BroughtInBy(IEnumerable<RoomUser> users, int bringerId) =>
        users.Where(user => user?.GetClient()?.GetHabbo() is { IsInvisible: true } habbo && habbo.InvisibleBroughtById == bringerId && !IsStaff(habbo))
            .ToList();
}
