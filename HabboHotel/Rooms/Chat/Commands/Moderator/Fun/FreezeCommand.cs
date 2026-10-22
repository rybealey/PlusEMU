using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator.Fun;

/// <summary>
/// pixelrp: Builders may :freeze and :unfreeze themselves and nobody else
/// (Twist, 2026-10-22); Trial Moderators and up anybody in the room, as
/// before. A chat command rather than a target command so that no name can
/// mean "yourself" - CommandManager answers a target command typed with no
/// name before it ever runs.
/// </summary>
internal class FreezeCommand : IChatCommand
{
    /// <summary>Trial Moderator: from here up, anybody in the room; below it, only yourself.</summary>
    internal const int FreezeOthersRank = 3;

    private readonly IGameClientManager _gameClientManager;
    public string Key => "freeze";
    public string PermissionRequired => "command_freeze";

    public string Parameters => "%username%";

    public string Description => "Stop a user from walking - Builders can only freeze themselves.";

    public FreezeCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var target = Target(session, room, parameters, _gameClientManager, "freeze");
        if (target == null)
            return;
        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser != null)
            targetUser.Frozen = true;
        session.SendWhisper(target.Id == session.GetHabbo().Id ? "You have frozen yourself." : $"Successfully froze {target.Username}!");
    }

    /// <summary>
    /// Who a :freeze or :unfreeze is for. A Builder: themselves - no name, or
    /// their own. Trial Moderators and up: the name typed, else the HUD target,
    /// as the target commands find one. In this room either way. Null, having
    /// told them, when there is nobody it may be.
    /// </summary>
    internal static Habbo? Target(GameClient session, Room room, string[] parameters, IGameClientManager clients, string verb)
    {
        var habbo = session.GetHabbo();
        var mayDoOthers = habbo.Rank >= FreezeOthersRank;
        // "x" is the client's shorthand for the HUD target; one that arrives
        // bare means nobody was selected (CommandManager).
        var typed = parameters.Length > 0 && !parameters[0].Equals("x", StringComparison.OrdinalIgnoreCase) ? parameters[0] : null;
        Habbo? target;
        if (typed != null)
        {
            target = clients.GetClientByUsername(typed)?.GetHabbo();
            if (target == null)
            {
                session.SendWhisper($"User {typed} seems to be offline.");
                return null;
            }
        }
        else if (!mayDoOthers)
            target = habbo;
        else
        {
            target = habbo.RpHudTargetId > 0 ? clients.GetClientByUserId(habbo.RpHudTargetId)?.GetHabbo() : null;
            if (target == null)
            {
                session.SendWhisper("No username specified.");
                return null;
            }
        }
        if (target.Id != habbo.Id && !mayDoOthers)
        {
            session.SendWhisper($"You can only {verb} yourself.");
            return null;
        }
        if (target.CurrentRoom != room)
        {
            session.SendWhisper($"You must be in the same room as {target.Username} to execute this command.");
            return null;
        }
        return target;
    }
}
