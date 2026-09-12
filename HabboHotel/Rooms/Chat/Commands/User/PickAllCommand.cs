using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

internal class PickAllCommand : IChatCommand
{
    private readonly IDatabase _database;
    public string Key => "pickall";
    public string PermissionRequired => "command_pickall";

    public string Parameters => "";

    public string Description => "Picks up all of your furniture in this room.";

    public PickAllCommand(IDatabase database)
    {
        _database = database;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        // pixelrp: rights rather than ownership. RemoveItems() below only takes
        // furniture whose user_id is the caller's, so a builder working in
        // somebody else's room can clear their OWN pieces to start a layout
        // again - which is the whole point of the command - without being able
        // to touch a single thing belonging to the room's owner.
        if (!room.CheckRights(session, false, true))
        {
            session.SendWhisper("You need rights in this room to pick furniture up here.");
            return;
        }
        var mine = room.GetRoomItemHandler().GetWallAndFloor.Count(item => item != null && item.UserId == session.GetHabbo().Id);
        if (mine == 0)
        {
            session.SendWhisper("You have no furniture in this room.");
            return;
        }
        room.GetRoomItemHandler().RemoveItems(session);
        room.GetGameMap().GenerateMaps();
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `items` SET `room_id` = '0' WHERE `room_id` = @RoomId AND `user_id` = @UserId");
            dbClient.AddParameter("RoomId", room.Id);
            dbClient.AddParameter("UserId", session.GetHabbo().Id);
            dbClient.RunQuery();
        }
        session.SendWhisper($"Picked up {mine} {(mine == 1 ? "item" : "items")}.");
        var items = room.GetRoomItemHandler().GetWallAndFloor.ToList();
        if (items.Count > 0)
            session.SendWhisper("There are still more items in this room, manually remove them or use :ejectall to eject them!");
        session.Send(new FurniListUpdateComposer());
    }
}
