using Dapper;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notifications;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: add or remove a member on a shared album (owner only). Added
/// members must be friends of the owner. Removing a member also removes the
/// photos they contributed to the album (their library copies stay).
/// Replies with the refreshed album list, and sends it to everyone else on the
/// album so an invite (or a removal) lands without reopening the app. The
/// invited member is notified on their phone.
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
        var albumName = "";
        // Whoever could see the album BEFORE the change, so a member who is
        // removed also gets a list without it.
        var before = new List<int>();
        using (var connection = _database.Connection())
        {
            var access = await RpAlbumLibrary.GetAlbumAccess(connection, albumId, habbo.Id);
            if (!access.Exists || !access.IsOwner || !access.IsShared)
                return;
            albumName = await connection.ExecuteScalarAsync<string>(
                "SELECT `name` FROM `camera_web_albums` WHERE `id` = @albumId LIMIT 1", new { albumId }) ?? "";
            before = await RpAlbumLibrary.GetAudience(connection, albumId);
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

        // Being invited to a shared album is the notification; being removed
        // is not (there is nothing to go and look at).
        if (add)
            NotificationUtility.Push(userId, NotificationUtility.Photos, "album_invite", albumName, habbo.Username, albumId);

        // Everyone who could see the album before or after, minus the owner,
        // who already has the fresh list above.
        using (var connection = _database.Connection())
            await RpAlbumLibrary.SendAlbumListTo(_database, before.Concat(await RpAlbumLibrary.GetAudience(connection, albumId)).Where(id => id != habbo.Id));
    }
}
