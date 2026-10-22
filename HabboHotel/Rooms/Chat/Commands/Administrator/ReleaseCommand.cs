using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Administrator;

/// <summary>
/// pixelrp: :release - an Administrator lets a prisoner out of jail early, the
/// City Panel's release from the chat box (Twist, 2026-10-22). Through the same
/// JailState.StaffRelease, so the record is closed, the uniform comes off and
/// any trial still to come is called off just the same.
///
/// THEY CAN LET THEMSELVES OUT. With no name, an Administrator in jail is
/// released; anyone else gets their HUD target, as the target commands do.
/// Being in jail, cuffed or out cold stops none of this: the command is not a
/// player command (CommandManager) and not on JailState's block list.
///
/// Online players only - a prisoner's clock stops at logout, and the City
/// Panel already ends an offline sentence. Not mid-trial, as the City Panel.
/// </summary>
internal class ReleaseCommand : IChatCommand
{
    private readonly IGameClientManager _gameClientManager;

    public string Key => "release";
    public string PermissionRequired => "command_release";

    public string Parameters => "%username%";

    public string Description => "Release a prisoner from jail - yourself, with no name, while you are in jail.";

    public ReleaseCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var staff = session.GetHabbo();
        if (staff == null)
            return;

        // "x" is the client's shorthand for the HUD target; one that arrives
        // bare means nobody was selected (CommandManager).
        var typed = parameters.Length > 0 && !parameters[0].Equals("x", StringComparison.OrdinalIgnoreCase) ? parameters[0] : null;
        GameClient? target;
        if (typed != null)
        {
            target = _gameClientManager.GetClientByUsername(typed);
            if (target?.GetHabbo() == null)
            {
                session.SendWhisper($"User {typed} seems to be offline.");
                return;
            }
        }
        else if (JailState.IsJailed(staff.Id))
            target = session;
        else
        {
            target = staff.RpHudTargetId > 0 ? _gameClientManager.GetClientByUserId(staff.RpHudTargetId) : null;
            if (target?.GetHabbo() == null)
            {
                session.SendWhisper("No username specified.");
                return;
            }
        }

        var prisoner = target.GetHabbo();
        var self = prisoner.Id == staff.Id;
        if (!JailState.IsJailed(prisoner.Id))
        {
            session.SendWhisper(self ? "You are not in jail." : $"{prisoner.Username} is not in jail.");
            return;
        }
        if (JuryTrials.InCourtNow(prisoner.Id))
        {
            session.SendWhisper(self ? "You are on trial." : $"{prisoner.Username} is on trial.");
            return;
        }
        if (!JailState.StaffRelease(prisoner.Id, target, self ? "You have released yourself from jail." : JailState.StaffReleasedWhisper))
            return;
        if (!self)
            session.SendWhisper($"You have released {prisoner.Username} from jail.");
    }
}
