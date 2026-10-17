using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// pixelrp: a bin to search through (BinSearch) - in a PixelRP-owned room.
/// Anywhere else it is the plain furni it was: rights open and shut it.
/// </summary>
public class InteractorBinSearch : IFurniInteractor
{
    private static readonly InteractorGenericSwitch Plain = new();

    public void OnPlace(GameClient session, Item item) => BinSearch.OnLoaded(item, placed: true);

    // Loading is RoomItemHandling.LoadFurniture's: BinSearch.OnLoaded(item, room).

    public void OnRemove(GameClient session, Item item) => BinSearch.OnRemoved(item);

    public void OnTrigger(GameClient session, Item item, int request, bool hasRights)
    {
        if (BinSearch.IsSearchable(item))
            BinSearch.Start(session, item);
        else
            Plain.OnTrigger(session, item, request, hasRights);
    }

    public void OnWiredTrigger(Item item)
    {
        if (!BinSearch.IsSearchable(item))
            Plain.OnWiredTrigger(item);
    }
}
