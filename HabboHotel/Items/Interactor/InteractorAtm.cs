using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Users.Banking;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// pixelrp: the cash machine.
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
/// Deliberately stateless. Unlike the vending machine there is no animation to
/// hold and no InteractingUser to pin, so two people can use the same machine
/// at once - which is what a room with one ATM and a queue needs.
/// </summary>
public class InteractorAtm : IFurniInteractor
{
    public void OnPlace(GameClient session, Item item) { }

    public void OnRemove(GameClient session, Item item) { }

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
            // Refused at the machine rather than opened onto an empty screen:
            // an account is opened in the Wallet, which is where the card and
            // the savings account live and where the choice belongs.
            session.SendWhisper("You need a bank account before you can use a cash machine. Open one from your Wallet.");
            return;
        }

        AtmSessions.Start(habbo.Id, room.Id);
        session.Send(new RpAtmOpenComposer(account, habbo.Credits));
    }

    public void OnWiredTrigger(Item item) { }
}
