using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// pixelrp: the jukebox behaviour, on any furni.
///
/// Using it opens the jukebox for the player who used it (RpJukeboxOpenComposer)
/// and changes nothing else. Its state is the room's play state - "1" while
/// this room's station plays - and RoomJukeboxManager.SyncJukeboxItemState owns
/// it, so a click must not toggle it the way the default switch did.
/// </summary>
public class InteractorJukebox : IFurniInteractor
{
    public void OnTrigger(GameClient session, Item item, int request, bool hasRights)
    {
        if (session?.GetHabbo() == null || item == null)
            return;
        session.Send(new RpJukeboxOpenComposer());
    }
}
