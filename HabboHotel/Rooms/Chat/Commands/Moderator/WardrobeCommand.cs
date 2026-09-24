using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :wd - open the clothes editor, anywhere. Staff only. Players
/// dress through the clothing store and dressing booths; this is the same
/// editor a booth opens (the avatar-editor/show link), without the booth.
/// </summary>
internal class WardrobeCommand : IChatCommand
{
    public string Key => "wd";
    public string PermissionRequired => "command_wd";

    public string Parameters => "";

    public string Description => "Open the clothes editor.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        session.Send(new InClientLinkComposer("avatar-editor/show"));
    }
}
