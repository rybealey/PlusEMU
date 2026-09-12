using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// pixelrp: sets a placed item's opacity from the infostand's build tools.
///
/// Rights are the same ones the tools themselves are gated on - if you cannot
/// move an item you cannot fade it - and the value is clamped here rather than
/// trusted, because a client is free to send anything. 10 is the floor: fully
/// invisible furni would be a way to hide things from other people rather than
/// a building aid.
///
/// Persisted immediately and broadcast to the whole room, so everyone sees the
/// same room and it survives a reload.
/// </summary>
internal class RpSetFurniAlphaEvent : IPacketEvent
{
    private const int MinimumAlpha = 10;
    private const int FullyOpaque = 100;

    private readonly IDatabase _database;

    public RpSetFurniAlphaEvent(IDatabase database) => _database = database;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var itemId = packet.ReadInt();
        var alpha = Math.Clamp(packet.ReadInt(), MinimumAlpha, FullyOpaque);
        var room = session.GetHabbo()?.CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        if (!room.CheckRights(session, true))
            return Task.CompletedTask;
        var item = room.GetRoomItemHandler().GetItem((uint)itemId);
        if (item == null || item.RoomId != room.Id)
            return Task.CompletedTask;
        if (item.Alpha == alpha)
            return Task.CompletedTask;

        // pixelrp: the previous opacity, for :undo. Past the equality check above,
        // so dragging the slider back to where it already was records nothing.
        var builder = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (builder != null)
            builder.LastFurniUndo = FurniUndoState.Capture(item);

        item.Alpha = alpha;
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `items` SET `alpha` = @alpha WHERE `id` = @itemId LIMIT 1");
            dbClient.AddParameter("alpha", alpha);
            dbClient.AddParameter("itemId", itemId);
            dbClient.RunQuery();
        }
        room.SendPacket(new RpFurniAlphaComposer(item));
        return Task.CompletedTask;
    }
}
