using Dapper;
using Plus.Database;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: the phone's Photos app saved an edit (crop/zoom) of a photo.
/// The edited image is stored as a brand-new file pair and the player's
/// camera_web row is repointed at it; the taken-at timestamp is kept.
/// The original file stays for any photo furni printed from it. Replies
/// with the refreshed library.
/// </summary>
internal class RpUpdatePhotoEvent : IPacketEvent
{
    private const int MaxPhotoBytes = 2_000_000;

    private readonly ICameraPhotoManager _cameraPhotoManager;
    private readonly IDatabase _database;

    public RpUpdatePhotoEvent(ICameraPhotoManager cameraPhotoManager, IDatabase database)
    {
        _cameraPhotoManager = cameraPhotoManager;
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var photoId = packet.ReadInt();
        var length = packet.ReadInt();
        if (length <= 0 || length > MaxPhotoBytes)
            return;
        var bytes = new byte[length];
        packet.ReadBytes(bytes);

        var habbo = session.GetHabbo();
        if (habbo == null)
            return;

        using (var connection = _database.Connection())
        {
            var owned = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM `camera_web` WHERE `id` = @photoId AND `user_id` = @userId",
                new { photoId, userId = habbo.Id });
            if (owned == 0)
                return;

            var url = _cameraPhotoManager.StoreEditedPhoto(bytes);
            await connection.ExecuteAsync(
                "UPDATE `camera_web` SET `url` = @url WHERE `id` = @photoId AND `user_id` = @userId",
                new { url, photoId, userId = habbo.Id });
        }
        await RpPhotoLibrary.SendPhotoList(_database, session);
    }
}
