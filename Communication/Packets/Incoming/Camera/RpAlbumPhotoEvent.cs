using Dapper;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: add or remove a photo in an album. Adding requires album access
/// (owner, or member of a shared album) and the photo must be the session
/// user's own. Removing is allowed for the photo's contributor or the album
/// owner. Replies with the album's refreshed photos + the album list (counts).
/// </summary>
internal class RpAlbumPhotoEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpAlbumPhotoEvent(IDatabase database)
    {
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var albumId = packet.ReadInt();
        var photoId = packet.ReadInt();
        var add = packet.ReadBool();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        using (var connection = _database.Connection())
        {
            var access = await RpAlbumLibrary.GetAlbumAccess(connection, albumId, habbo.Id);
            if (!access.CanView)
                return;
            if (add)
            {
                var ownsPhoto = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM `camera_web` WHERE `id` = @photoId AND `user_id` = @userId",
                    new { photoId, userId = habbo.Id }) > 0;
                if (!ownsPhoto)
                    return;
                await connection.ExecuteAsync(
                    "INSERT IGNORE INTO `camera_web_album_photos` (`album_id`, `photo_id`) VALUES (@albumId, @photoId)",
                    new { albumId, photoId });
            }
            else
            {
                // Contributors pull their own photos; the album owner can
                // remove anything.
                if (access.IsOwner)
                    await connection.ExecuteAsync(
                        "DELETE FROM `camera_web_album_photos` WHERE `album_id` = @albumId AND `photo_id` = @photoId",
                        new { albumId, photoId });
                else
                    await connection.ExecuteAsync(
                        "DELETE ap FROM `camera_web_album_photos` ap JOIN `camera_web` cw ON cw.`id` = ap.`photo_id` WHERE ap.`album_id` = @albumId AND ap.`photo_id` = @photoId AND cw.`user_id` = @userId",
                        new { albumId, photoId, userId = habbo.Id });
            }
        }
        await RpAlbumLibrary.SendAlbumPhotos(_database, session, albumId);
        await RpAlbumLibrary.SendAlbumList(_database, session);
    }
}
