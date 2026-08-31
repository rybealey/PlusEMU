using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :startwork - begin a shift at your corporation. Pay lands every
/// 10 minutes worked; progress persists across sessions.
/// </summary>
internal class StartWorkCommand : IChatCommand
{
    public string Key => "startwork";
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Start your shift at your corporation.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        ShiftManager.StartShift(session);
    }
}
