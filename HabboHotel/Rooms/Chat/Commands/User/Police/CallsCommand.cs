using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police: :calls - open the 911 Emergency Calls window. It opens by
/// itself on clock-in and on every new call; this brings it back after its X
/// put it away. For on-duty officers and on-duty staff (City Government),
/// who take police calls too.
///
/// Police only (Twist, 2026-10-18): Paramedic Dispatch comes up by itself
/// when a call is offered and has no :calls of its own.
/// </summary>
internal class CallsCommand : IChatCommand
{
    public string Key => "calls";
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Open the 911 Dispatch window.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!PoliceUtility.RequireOnDuty(session, "see 911 Dispatch"))
            return;
        EmergencyCalls.Reopen(session);
    }
}
