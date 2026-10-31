using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :cp - open the Corporation Panel for your own corporation. Only
/// ranks with panel_access (migration 334, management by default); anyone
/// else is told why. The window asks for its data once open, and every one
/// of those packets is checked again (CorpPanel.Handle).
/// </summary>
internal class CorpPanelCommand : IChatCommand
{
    public string Key => "cp";
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Open your corporation's management panel.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (CorpPanel.Require(session, whisper: true) == null)
            return;
        session.Send(new InClientLinkComposer("corp-panel/show"));
    }
}
