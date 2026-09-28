using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police: :fb - throw a flashbang. The same as clicking one in the
/// backpack: both go through Flashbang.Throw, which holds every rule (on duty,
/// carrying one, who it can catch).
/// </summary>
internal class FlashbangCommand : IChatCommand
{
    public string Key => "fb";
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Throw a flashbang, stunning everyone around you.";

    public void Execute(GameClient session, Room room, string[] parameters) => Flashbang.Throw(session, room);
}
