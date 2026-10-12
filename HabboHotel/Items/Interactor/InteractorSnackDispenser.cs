using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.Trading;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// pixelrp: a snack vending machine - the Muse Vending Machine (migration 249).
///
/// Using it puts a Snack in the player's backpack and switches the machine to
/// state 1, whose bundle animation drops a bag into the pickup slot, holds it
/// there for two seconds and settles back on the idle frame by itself;
/// Item.ProcessUpdates puts the state back to 0 a little later, so the next
/// use plays it again. One Snack per use, and a machine still dispensing
/// takes no new order. Eating the Snack is RpUseItemEvent's: Apple Crisps in
/// hand while the energy bar fills.
///
/// Walk-up and the refusals are the police locker's (InteractorPoliceReplenish):
/// cuffed, out cold, jailed or mid-trade gets nothing, and so does a full
/// backpack - in which case the machine does not play either.
/// </summary>
public class InteractorSnackDispenser : IFurniInteractor
{
    private const string SnackItem = "snack";

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
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        var user = item.GetRoom()?.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (user == null)
            return;
        // Like any vending machine: walk up to it first, then use it.
        if (!Gamemap.TilesTouching(user.X, user.Y, item.GetX, item.GetY))
        {
            user.MoveTo(item.SquareInFront);
            return;
        }
        // Face the machine, but never turn someone mid-step.
        if (!user.IsWalking)
            user.SetRot(Rotation.Calculate(user.X, user.Y, item.GetX, item.GetY), false);

        if (item.LegacyDataString == "1")
        {
            session.SendWhisper("The machine is still dispensing.");
            return;
        }
        if (PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("Your hands are cuffed.");
            return;
        }
        if (KnockedOut.Refuse(session))
            return;
        if (JailState.IsJailed(habbo.Id))
        {
            session.SendWhisper("You can't do that in jail.");
            return;
        }
        // The backpack is frozen while a trade is open.
        if (RpTrades.IsTrading(habbo.Id))
        {
            session.SendWhisper(RpTrades.FrozenMessage);
            return;
        }
        if (habbo.AddRpItem(SnackItem) < 0)
        {
            session.SendWhisper("Your backpack is full.");
            return;
        }
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        session.SendWhisper("You have received a snack.");
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
