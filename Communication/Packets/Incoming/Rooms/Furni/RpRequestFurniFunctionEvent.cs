using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// pixelrp: opens the Function window on a placed item's DEFINITION.
///
/// The client sends the item it has selected; what comes back describes the
/// definition behind it, because that is what the window edits - a change
/// lands on every copy of the furni, not on the one that was clicked.
///
/// The blast-radius counts are gathered here rather than in the client so the
/// warning states a fact rather than an estimate. They cover placed items
/// only: inventory copies take the new behaviour when they are placed, so
/// counting them would overstate what changes right now.
/// </summary>
internal class RpRequestFurniFunctionEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpRequestFurniFunctionEvent(IDatabase database) => _database = database;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var itemId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || !habbo.Permissions.HasCommand("rp_furni_function"))
            return Task.CompletedTask;
        var room = habbo.CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        var item = room.GetRoomItemHandler().GetItem((uint)itemId);
        if (item?.Definition == null)
            return Task.CompletedTask;

        var definition = item.Definition;
        var placedCopies = 0;
        var roomCount = 0;
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT COUNT(*) AS `copies`, COUNT(DISTINCT `room_id`) AS `rooms` " +
                              "FROM `items` WHERE `base_item` = @definitionId AND `room_id` > 0");
            dbClient.AddParameter("definitionId", definition.Id);
            var row = dbClient.GetRow();
            if (row != null)
            {
                placedCopies = Convert.ToInt32(row["copies"]);
                roomCount = Convert.ToInt32(row["rooms"]);
            }
        }

        session.Send(new RpFurniFunctionComposer(definition, placedCopies, roomCount));
        return Task.CompletedTask;
    }
}
