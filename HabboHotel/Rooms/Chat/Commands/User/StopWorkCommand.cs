using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :stopwork - end your shift, banking progress toward your next pay.
/// </summary>
internal class StopWorkCommand : IChatCommand
{
    public string Key => "stopwork";
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "End your shift.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        ShiftManager.StopShift(session);
    }
}
