using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :zara opens the Clothing Store window. The window itself asks
/// for the shelf once it shows, so the command only has to open it.
/// </summary>
internal class ZaraCommand : IChatCommand
{
    public string Key => "zara";
    public string PermissionRequired => "command_zara";

    public string Parameters => "";

    public string Description => "Open the clothing store.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (session.GetHabbo() == null)
            return;
        session.Send(new RpOpenClothingStoreComposer());
    }
}
