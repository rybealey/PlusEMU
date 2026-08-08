using Dapper;
using Plus.Communication.Packets.Outgoing.Camera;
using Plus.Database;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

internal class PublishPhotoEvent : IPacketEvent
{
    private readonly ICameraPhotoManager _cameraPhotoManager;
    private readonly IDatabase _database;

    public PublishPhotoEvent(ICameraPhotoManager cameraPhotoManager, IDatabase database)
    {
        _cameraPhotoManager = cameraPhotoManager;
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!_cameraPhotoManager.TryGetPending(session.GetHabbo().Id, out var pending))
        {
            session.Send(new CameraPublishStatusMessageComposer(false, ""));
            return;
        }

        using var connection = _database.Connection();
        await connection.ExecuteAsync(
            "INSERT INTO `camera_web` (`user_id`, `room_id`, `timestamp`, `url`, `visible`) VALUES (@userId, @roomId, @timestamp, @url, 1)",
            new { userId = session.GetHabbo().Id, roomId = pending.RoomId, timestamp = pending.TakenUnixMs / 1000, url = pending.Url });

        session.Send(new CameraPublishStatusMessageComposer(true, pending.Url));
    }
}
