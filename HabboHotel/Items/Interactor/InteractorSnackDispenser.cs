using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// pixelrp: a vending machine that SHOWS a snack being dispensed - the Muse
/// Vending Machine (migration 249). Using it switches it to state 1, whose
/// bundle animation drops a bag into the pickup slot, holds it there for two
/// seconds and settles back on the idle frame by itself; Item.ProcessUpdates
/// puts the state back to 0 a little later, so the next use plays it again.
///
/// Nothing is handed out. Snacks are sold by The Muse (CityEconomy), and a
/// free one from a machine would undercut that.
/// </summary>
public class InteractorSnackDispenser : IFurniInteractor
{
    // The animation is four drop frames of 4 beats and the bag held for 48, at
    // the client's 24 beats a second: 2.67s. Room cycles are 500ms, so 7 (3.0 to
    // 3.5s, depending where in a cycle the use lands) never cut it short.
    public const int ResetCycles = 7;

    public void OnPlace(GameClient session, Item item)
    {
        item.LegacyDataString = "0";
        item.UpdateNeeded = true;
    }

    public void OnRemove(GameClient session, Item item)
    {
        item.LegacyDataString = "0";
    }

    public void OnTrigger(GameClient session, Item item, int request, bool hasRights)
    {
        if (session == null || item.LegacyDataString == "1") return;
        var user = item.GetRoom().GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null) return;
        // Like any vending machine: walk up to it first, then use it.
        if (!Gamemap.TilesTouching(user.X, user.Y, item.GetX, item.GetY))
        {
            user.MoveTo(item.SquareInFront);
            return;
        }
        // Face the machine, but never turn someone mid-step.
        if (!user.IsWalking)
            user.SetRot(Rotation.Calculate(user.X, user.Y, item.GetX, item.GetY), false);
        Dispense(item);
    }

    public void OnWiredTrigger(Item item)
    {
        if (item.LegacyDataString != "1") Dispense(item);
    }

    private static void Dispense(Item item)
    {
        item.LegacyDataString = "1";
        item.UpdateState(false, true);
        item.RequestUpdate(ResetCycles, true);
    }
}
