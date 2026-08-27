using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

/// <summary>
/// pixelrp: sets the room's roleplay zone type (Room settings > Roleplay >
/// Zone Type). Owner only; persisted immediately.
/// </summary>
internal class RpRoomZoneSaveEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpRoomZoneSaveEvent(IDatabase database)
    {
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var isSafeZone = packet.ReadInt() == 1;
        var room = session.GetHabbo()?.CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        if (!room.CheckRights(session, true))
            return Task.CompletedTask;
        room.IsSafeZone = isSafeZone;
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `rooms` SET `is_safe_zone` = @safe WHERE `id` = @roomId LIMIT 1");
            dbClient.AddParameter("safe", isSafeZone ? "1" : "0");
            dbClient.AddParameter("roomId", room.Id);
            dbClient.RunQuery();
        }
        session.Send(new RpRoomZoneComposer(room.Id, room.IsSafeZone));
        return Task.CompletedTask;
    }
}
