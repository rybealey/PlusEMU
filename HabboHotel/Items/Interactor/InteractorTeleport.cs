using Plus.HabboHotel.GameClients;
using Plus.Utilities;

namespace Plus.HabboHotel.Items.Interactor;

public class InteractorTeleport : IFurniInteractor
{
    public void OnPlace(GameClient session, Item item)
    {
        item.LegacyDataString = "0";
        if (item.InteractingUser != 0)
        {
            var user = item.GetRoom().GetRoomUserManager().GetRoomUserByHabbo(item.InteractingUser);
            if (user != null)
            {
                user.ClearMovement(true);
                user.AllowOverride = false;
                user.CanWalk = true;
            }
            item.InteractingUser = 0;
        }
        if (item.InteractingUser2 != 0)
        {
            var user = item.GetRoom().GetRoomUserManager().GetRoomUserByHabbo(item.InteractingUser2);
            if (user != null)
            {
                user.ClearMovement(true);
                user.AllowOverride = false;
                user.CanWalk = true;
            }
            item.InteractingUser2 = 0;
        }
    }

    public void OnRemove(GameClient session, Item item)
    {
        item.LegacyDataString = "0";
        if (item.InteractingUser != 0)
        {
            var user = item.GetRoom().GetRoomUserManager().GetRoomUserByHabbo(item.InteractingUser);
            if (user != null) user.UnlockWalking();
            item.InteractingUser = 0;
        }
        if (item.InteractingUser2 != 0)
        {
            var user = item.GetRoom().GetRoomUserManager().GetRoomUserByHabbo(item.InteractingUser2);
            if (user != null) user.UnlockWalking();
            item.InteractingUser2 = 0;
        }
    }

    public void OnTrigger(GameClient session, Item item, int request, bool hasRights)
    {
        if (item == null || item.GetRoom() == null || session == null || session.GetHabbo() == null)
            return;
        var user = item.GetRoom().GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null)
            return;
        user.LastInteraction = UnixTimestamp.GetNow();

        // pixelrp: the room the twin stands in starts loading now, while the
        // player walks in and the booth counts down, instead of once the trip
        // has begun and they have left this room. Found on the warm-up worker:
        // finding it may ask the database.
        var fromRoom = item.GetRoom();
        var boothId = item.Id;
        PlusEnvironment.Game.RoomManager.WarmInBackground(() =>
        {
            var twinId = ItemTeleporterFinder.GetLinkedTele(boothId);
            return twinId == 0 ? Array.Empty<uint>() : new[] { ItemTeleporterFinder.GetTeleRoomId(twinId, fromRoom) };
        });

        // Alright. But is this user in the right position?
        if (user.Coordinate == item.Coordinate || user.Coordinate == item.SquareInFront)
        {
            // Fine. But is this tele even free?
            if (item.InteractingUser != 0) return;
            if (!user.CanWalk || session.GetHabbo().IsTeleporting || session.GetHabbo().TeleporterId != 0 ||
                user.LastInteraction + 2 - UnixTimestamp.GetNow() < 0)
                return;
            user.TeleDelay = 2;
            item.InteractingUser = user.GetClient().GetHabbo().Id;
        }
        else if (user.CanWalk) user.MoveTo(item.SquareInFront);
    }

    public void OnWiredTrigger(Item item) { }
}