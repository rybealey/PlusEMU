using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :invisible - out of sight of everyone who isn't invisible too
/// (Invisibility). Typed again, it turns off.
///
/// :invisible name brings someone in the room into it, staff or not, to talk
/// with them where nobody else can see - only once you are invisible yourself.
/// On someone already invisible it turns them visible, whoever brought them.
///
/// An IChatCommand that finds the name itself, as :wander does: an
/// ITargetChatCommand can't run without a target.
/// </summary>
internal class InvisibleCommand : IChatCommand
{
    public string Key => "invisible";

    public string PermissionRequired => "command_invisible";

    public string Parameters => "%username%";

    public string Description => "Go invisible, or bring someone in the room into it with you.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var manager = room?.GetRoomUserManager();
        var me = manager?.GetRoomUserByHabbo(session.GetHabbo().Id);
        if (me == null)
        {
            session.SendWhisper("You need to be in a room to do that.");
            return;
        }

        var target = me;
        if (parameters.Length > 0)
        {
            var name = string.Join(" ", parameters);
            target = manager.GetRoomUserByHabbo(name);
            if (target == null || target.IsBot || target.GetClient()?.GetHabbo() == null)
            {
                session.SendWhisper($"{name} is not in this room.");
                return;
            }
        }

        if (target == me)
        {
            if (me.IsInvisible)
            {
                Invisibility.Disable(room, me);
                session.SendWhisper("You're visible again.");
            }
            else
            {
                Invisibility.Enable(room, me);
                session.SendWhisper("You're invisible. Only others who are invisible can see or hear you - :invisible again to come back.");
            }
            return;
        }

        var targetName = target.GetUsername();
        if (target.IsInvisible)
        {
            Invisibility.Disable(room, target);
            session.SendWhisper($"{targetName} is visible again.");
            target.GetClient().SendWhisper($"{session.GetHabbo().Username} made you visible again.");
            return;
        }

        if (!me.IsInvisible)
        {
            session.SendWhisper("Go invisible first to bring someone in.");
            return;
        }

        Invisibility.Enable(room, target, me);
        session.SendWhisper($"{targetName} is invisible with you - :invisible {targetName} brings them back.");
        target.GetClient().SendWhisper(Invisibility.IsStaff(target.GetClient().GetHabbo())
            ? $"{session.GetHabbo().Username} made you invisible. Only others who are invisible can see or hear you - :invisible to come back."
            : $"{session.GetHabbo().Username} made you invisible. Only others who are invisible can see or hear you, until they bring you back.");
    }
}
