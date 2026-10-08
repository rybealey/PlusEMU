using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police: :ps (user) - pepper spray. The same as clicking the can in
/// the backpack with a target selected: both go through PepperSpray.Spray,
/// which holds every rule (on duty, carrying a can, who can be sprayed).
/// </summary>
internal class PepperSprayCommand : ITargetChatCommand
{
    public string Key => "ps";
    public string PermissionRequired => "";

    public string Parameters => "%target%";

    public string Description => "Pepper spray a user, sending them stumbling back.";

    public bool MustBeInSameRoom => true;

    public bool IsRanged => true;

    public string NoTargetMessage => "No target selected.";

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        PepperSpray.Spray(session, room, target);
        return Task.CompletedTask;
    }
}
