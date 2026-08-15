using Plus.Communication.Packets.Outgoing.Navigator;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Action;

internal class GiveRoomScoreEvent : RoomPacketEvent
{
    private readonly IDatabase _database;

    public GiveRoomScoreEvent(IDatabase database)
    {
        _database = database;
    }
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        // Room likes/ratings are disabled hotel-wide. Ignore every rate packet,
        // including crafted/injected ones: the like button is removed from the
        // client, and no score may be registered here. _database is retained so
        // the handler's DI registration is unchanged.
        return Task.CompletedTask;
    }
}