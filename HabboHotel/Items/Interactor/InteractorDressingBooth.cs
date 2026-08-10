using System.Drawing;
using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Items.Interactor;

public class InteractorDressingBooth : IFurniInteractor
{
    public void OnPlace(GameClient session, Item item) { }

    public void OnRemove(GameClient session, Item item)
    {
        // Booth picked up from under a standing user: close their editor.
        var room = item.GetRoom();
        if (room == null)
            return;
        foreach (var user in room.GetGameMap().GetRoomUsers(new Point(item.GetX, item.GetY)))
            user.GetClient()?.Send(new InClientLinkComposer("avatar-editor/hide"));
    }

    public void OnTrigger(GameClient session, Item item, int request, bool hasRights) { }

    public void OnWiredTrigger(Item item) { }
}
