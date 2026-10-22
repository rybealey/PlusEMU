using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator.Fun;

/// <summary>
/// pixelrp: Builders only on themselves, as :freeze (Twist, 2026-10-22) - who
/// it is for is FreezeCommand.Target.
/// </summary>
internal class UnFreezeCommand : IChatCommand
{
    private readonly IGameClientManager _gameClientManager;
    public string Key => "unfreeze";
    public string PermissionRequired => "command_unfreeze";

    public string Parameters => "%username%";

    public string Description => "Let a user walk again - Builders can only unfreeze themselves.";

    public UnFreezeCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var target = FreezeCommand.Target(session, room, parameters, _gameClientManager, "unfreeze");
        if (target == null)
            return;
        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser != null)
            targetUser.Frozen = false;
        session.SendWhisper(target.Id == session.GetHabbo().Id ? "You have unfrozen yourself." : $"Successfully unfroze {target.Username}!");
    }
}
