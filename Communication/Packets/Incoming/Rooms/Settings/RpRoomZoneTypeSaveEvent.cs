using Plus.Communication.Packets.Outgoing.Rooms.Settings;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

/// <summary>
/// pixelrp: sets the room's zone TYPE - 0 unsafe, 1 safe, 2 turf (Room
/// settings > Roleplay > Zoning). Staff only (Room.CanManageSettings) and persisted at once, exactly as
/// RpRoomZoneSaveEvent, which it supersedes for any client that sends it; that
/// one stays for clients that only know safe and unsafe.
///
/// A turf is stored as unsafe (is_safe_zone '0') plus is_turf '1', so it plays
/// as an unsafe room everywhere. A room that stops being a turf loses its
/// owner; one that becomes a turf repaints its group furni to the neutral pair.
/// </summary>
internal class RpRoomZoneTypeSaveEvent : IPacketEvent
{
    private const int Unsafe = 0;
    private const int Safe = 1;
    private const int Turf = 2;

    private readonly IDatabase _database;

    public RpRoomZoneTypeSaveEvent(IDatabase database)
    {
        _database = database;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var zone = packet.ReadInt();
        if (zone != Unsafe && zone != Safe && zone != Turf)
            return Task.CompletedTask;
        var room = session.GetHabbo()?.CurrentRoom;
        if (room == null)
            return Task.CompletedTask;
        if (!room.CanManageSettings(session))
            return Task.CompletedTask;

        var wasTurf = room.IsTurf;
        room.IsSafeZone = zone == Safe;
        room.IsTurf = zone == Turf;
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `rooms` SET `is_safe_zone` = @safe, `is_turf` = @turf WHERE `id` = @roomId LIMIT 1");
            dbClient.AddParameter("safe", room.IsSafeZone ? "1" : "0");
            dbClient.AddParameter("turf", room.IsTurf ? "1" : "0");
            dbClient.AddParameter("roomId", room.Id);
            dbClient.RunQuery();
        }

        // Release and Recolour both push the turf panel to everyone in the room;
        // any other change still has to tell them the zone moved.
        if (wasTurf && !room.IsTurf)
            TurfManager.Release(room.Id);
        else if (!wasTurf && room.IsTurf)
            TurfManager.Recolour(room);
        else
            TurfManager.Broadcast(room);

        session.Send(new RpRoomZoneComposer(room.Id, room.IsSafeZone));
        return Task.CompletedTask;
    }
}
