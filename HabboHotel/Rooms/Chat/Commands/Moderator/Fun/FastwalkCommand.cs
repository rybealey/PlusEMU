using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator.Fun;

internal class FastwalkCommand : IChatCommand
{
    public string Key => "fastwalk";
    public string PermissionRequired => "command_fastwalk";

    public string Parameters => "";

    public string Description => "Walk twice as fast until you turn it off or log out.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session.GetHabbo();
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (user == null)
            return;
        habbo.FastWalking = !habbo.FastWalking;
        // Movement V2 reads no FastWalking flag - its one per-walker speed is
        // the pace the ambulance run uses, so fast walk borrows that. It lands
        // on the next walk, never mid-stride. A new room's movement state
        // starts at walking pace, and AddAvatarToRoom sets this one again.
        MovementV2Bridge.SetWalkPace(user, habbo.FastWalking ? MovementSettings.EscortIntervalMs : MovementSettings.IntervalMs);
        session.SendWhisper(habbo.FastWalking
            ? "Fast walk is on. It starts from your next walk."
            : "Fast walk is off.");
    }
}