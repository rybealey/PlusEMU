using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Users.Banking;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// pixelrp: the ATM.
///
/// Using the furni opens the ATM screen for that player alone. It is the ONLY
/// way that screen appears - RpAtmTransactionEvent refuses unless this has run
/// and the player is still in the room - so the machine is a place you have to
/// walk to rather than a menu you carry.
///
/// The player has to be next to it, on the vending machine precedent: standing
/// across the room and clicking sends them walking instead of opening it. A
/// bank you can use from anywhere is not a bank, it is a button.
///
/// No InteractingUser to pin. Unlike the vending machine there is no animation
/// to hold, so two people can use the same machine at once - which is what a
/// room with one ATM and a queue needs. Who is at it lives in AtmSessions, and
/// the machine ticks while anybody is, to close the window of whoever steps
/// off the tile they used it from (AtmSessions.Tick).
/// </summary>
public class InteractorAtm : IFurniInteractor
{
    public void OnPlace(GameClient session, Item item) { }

    public void OnRemove(GameClient session, Item item) => AtmSessions.CloseAll(item);

    public void OnTrigger(GameClient session, Item item, int request, bool hasRights)
    {
        if (session == null || item == null)
            return;
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        var room = item.GetRoom();
        if (room == null)
            return;
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (user == null)
            return;

        if (!Gamemap.TilesTouching(user.X, user.Y, item.GetX, item.GetY))
        {
            user.MoveTo(item.SquareInFront);
            return;
        }

        user.SetRot(Rotation.Calculate(user.X, user.Y, item.GetX, item.GetY), false);

        var account = BankUtility.Get(habbo.Id);
        if (account == null)
        {
            // Refused at the machine rather than opened onto an empty screen.
            // The same words as :bal (Twist, 2026-10-23).
            session.SendWhisper("You do not have a bank account. Head to Mercury Bank to open one.");
            return;
        }

        AtmSessions.Start(habbo.Id, room.Id, item.Id, user.X, user.Y);
        // Start the machine ticking unless it already is for somebody else -
        // queued twice it would tick twice a cycle for as long as it is in use.
        if (item.UpdateCounter <= 0)
            item.RequestUpdate(1, true);
        session.Send(new RpAtmOpenComposer(account, habbo.Credits));
    }

    public void OnWiredTrigger(Item item) { }
}
