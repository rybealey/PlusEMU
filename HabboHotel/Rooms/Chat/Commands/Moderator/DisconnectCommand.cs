using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

internal class DisconnectCommand : ITargetChatCommand
{
    public string Key => "dc";
    public string PermissionRequired => "command_disconnect";

    public string Parameters => "%username%";

    public string Description => "Disconnects another user from the hotel.";

    public bool MustBeInSameRoom => false;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        // pixelrp: staff can only be disconnected by someone holding
        // mod_disconnect_any - the one typing it. This read the TARGET's right,
        // so nobody could ever disconnect a staff member.
        if (target.Permissions.HasRight("mod_tool") && !session.GetHabbo().Permissions.HasRight("mod_disconnect_any"))
        {
            session.SendWhisper("You are not allowed to Disconnect that user.");
            return Task.CompletedTask;
        }
        target.Client.Disconnect();
        return Task.CompletedTask;
    }
}