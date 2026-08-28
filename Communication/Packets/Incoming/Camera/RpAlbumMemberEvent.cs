using Dapper;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: add or remove a member on a shared album (owner only). Added
/// members must be friends of the owner. Removing a member also removes the
/// photos they contributed to the album (their library copies stay).
/// Replies with the refreshed album list.
/// </summary>
internal class RpAlbumMemberEvent : IPacketEvent
{
    private const int MaxMembers = 30;

    private readonly IDatabase _database;

    public RpAlbumMemberEvent(IDatabase database)
    {
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var albumId = packet.ReadInt();
        var userId = packet.ReadInt();
        var add = packet.ReadBool();
        var habbo = session.GetHabbo();
        if (habbo == null || userId <= 0 || userId == habbo.Id)
            return;
        using (var connection = _database.Connection())
        {
            var access = await RpAlbumLibrary.GetAlbumAccess(connection, albumId, habbo.Id);
            if (!access.Exists || !access.IsOwner || !access.IsShared)
                return;
            if (add)
            {
                if (habbo.Messenger.GetFriend(userId) == null)
                    return;
                var memberCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM `camera_web_album_members` WHERE `album_id` = @albumId",
                    new { albumId });
                if (memberCount >= MaxMembers)
                    return;
                await connection.ExecuteAsync(
                    "INSERT IGNORE INTO `camera_web_album_members` (`album_id`, `user_id`) VALUES (@albumId, @userId)",
                    new { albumId, userId });
            }
            else
            {
                var removed = await connection.ExecuteAsync(
                    "DELETE FROM `camera_web_album_members` WHERE `album_id` = @albumId AND `user_id` = @userId",
                    new { albumId, userId });
                if (removed > 0)
                    await connection.ExecuteAsync(
                        "DELETE ap FROM `camera_web_album_photos` ap JOIN `camera_web` cw ON cw.`id` = ap.`photo_id` WHERE ap.`album_id` = @albumId AND cw.`user_id` = @userId",
                        new { albumId, userId });
            }
        }
        await RpAlbumLibrary.SendAlbumList(_database, session);
        await RpAlbumLibrary.SendAlbumPhotos(_database, session, albumId);
    }
}
