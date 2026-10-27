using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator.Fun;

internal class FastwalkCommand : IChatCommand
{
    public string Key => "fastwalk";
    public string PermissionRequired => "command_fastwalk";

    public string Parameters => "";

    public string Description => "Walk twice as fast until you leave the room.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null)
            return;
        user.FastWalking = !user.FastWalking;
        // Movement V2 reads no FastWalking flag - its one per-walker speed is
        // the pace the ambulance run uses, so fast walk borrows that. It lands
        // on the next walk, never mid-stride, and a new room starts at normal
        // pace (and a new RoomUser with FastWalking off) by itself.
        MovementV2Bridge.SetWalkPace(user, user.FastWalking ? MovementSettings.EscortIntervalMs : MovementSettings.IntervalMs);
        session.SendWhisper(user.FastWalking
            ? "Fast walk is on. It starts from your next walk."
            : "Fast walk is off.");
    }
}