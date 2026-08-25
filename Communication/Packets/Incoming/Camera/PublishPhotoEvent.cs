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

        // Idempotency: TryMarkPublished flips the pending photo's Published
        // flag at most once, so a repeated Publish (double-click/retry) skips
        // the INSERT — avoiding a duplicate public camera_web row — but still
        // replies success with the same URL so the client's UI doesn't hang.
        if (_cameraPhotoManager.TryMarkPublished(session.GetHabbo().Id))
        {
            using var connection = _database.Connection();
            // pixelrp: if the photo was purchased first it already sits in the
            // player's private library (visible = 0) — publishing flips that
            // same row public instead of inserting a duplicate.
            var updated = await connection.ExecuteAsync(
                "UPDATE `camera_web` SET `visible` = 1 WHERE `user_id` = @userId AND `url` = @url",
                new { userId = session.GetHabbo().Id, url = pending.Url });
            if (updated == 0)
                await connection.ExecuteAsync(
                    "INSERT INTO `camera_web` (`user_id`, `room_id`, `timestamp`, `url`, `visible`) VALUES (@userId, @roomId, @timestamp, @url, 1)",
                    new { userId = session.GetHabbo().Id, roomId = pending.RoomId, timestamp = pending.TakenUnixMs / 1000, url = pending.Url });
        }

        session.Send(new CameraPublishStatusMessageComposer(true, pending.Url));
    }
}
