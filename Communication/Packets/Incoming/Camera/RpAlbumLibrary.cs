using System.Data;
using Dapper;
using Plus.Communication.Packets.Outgoing.Camera;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: shared replies + access checks for the phone Photos app's
/// albums. The album list is sent after every album mutation so the
/// client's Collections state always comes from one source.
/// </summary>
internal static class RpAlbumLibrary
{
    public record AlbumAccess(bool Exists, bool IsOwner, bool IsMember, bool IsShared)
    {
        public bool CanView => IsOwner || IsMember;
    }

    public static async Task<AlbumAccess> GetAlbumAccess(IDbConnection connection, int albumId, int userId)
    {
        var album = await connection.QuerySingleOrDefaultAsync<(int OwnerId, bool Shared)>(
            "SELECT `owner_id`, `is_shared` FROM `camera_web_albums` WHERE `id` = @albumId LIMIT 1",
            new { albumId });
        if (album.OwnerId <= 0)
            return new AlbumAccess(false, false, false, false);
        if (album.OwnerId == userId)
            return new AlbumAccess(true, true, false, album.Shared);
        var isMember = album.Shared && await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM `camera_web_album_members` WHERE `album_id` = @albumId AND `user_id` = @userId",
            new { albumId, userId }) > 0;
        return new AlbumAccess(true, false, isMember, album.Shared);
    }

    public static async Task SendAlbumList(IDatabase database, GameClient session)
    {
        var userId = session.GetHabbo().Id;
        using var connection = database.Connection();
        var albums = (await connection.QueryAsync<(int Id, string Name, bool Shared, int OwnerId, string OwnerName)>(
            "SELECT a.`id`, a.`name`, a.`is_shared`, a.`owner_id`, u.`username` FROM `camera_web_albums` a JOIN `users` u ON u.`id` = a.`owner_id` " +
            "WHERE a.`owner_id` = @userId OR a.`id` IN (SELECT `album_id` FROM `camera_web_album_members` WHERE `user_id` = @userId) ORDER BY a.`id` DESC",
            new { userId })).ToList();

        var photoCounts = new Dictionary<int, int>();
        var covers = new Dictionary<int, string>();
        var membersByAlbum = new Dictionary<int, List<RpAlbumListComposer.Member>>();
        if (albums.Count > 0)
        {
            var albumIds = albums.Select(album => album.Id).ToList();
            foreach (var row in await connection.QueryAsync<(int AlbumId, int Count)>(
                "SELECT `album_id`, COUNT(*) FROM `camera_web_album_photos` WHERE `album_id` IN @albumIds GROUP BY `album_id`",
                new { albumIds }))
                photoCounts[row.AlbumId] = row.Count;
            // Ascending scan; the last row per album wins = the latest-added
            // photo becomes the cover.
            foreach (var row in await connection.QueryAsync<(int AlbumId, string Url)>(
                "SELECT ap.`album_id`, cw.`url` FROM `camera_web_album_photos` ap JOIN `camera_web` cw ON cw.`id` = ap.`photo_id` WHERE ap.`album_id` IN @albumIds ORDER BY ap.`id` ASC",
                new { albumIds }))
                covers[row.AlbumId] = row.Url;
            foreach (var row in await connection.QueryAsync<(int AlbumId, int UserId, string Username)>(
                "SELECT m.`album_id`, m.`user_id`, u.`username` FROM `camera_web_album_members` m JOIN `users` u ON u.`id` = m.`user_id` WHERE m.`album_id` IN @albumIds",
                new { albumIds }))
            {
                if (!membersByAlbum.TryGetValue(row.AlbumId, out var members))
                    membersByAlbum[row.AlbumId] = members = new List<RpAlbumListComposer.Member>();
                members.Add(new RpAlbumListComposer.Member(row.UserId, row.Username));
            }
        }

        session.Send(new RpAlbumListComposer(albums
            .Select(album => new RpAlbumListComposer.Album(album.Id, album.Name, album.Shared, album.OwnerId, album.OwnerName,
                photoCounts.TryGetValue(album.Id, out var count) ? count : 0,
                covers.TryGetValue(album.Id, out var cover) ? cover : "",
                membersByAlbum.TryGetValue(album.Id, out var members) ? members : new List<RpAlbumListComposer.Member>()))
            .ToList()));
    }

    public static async Task SendAlbumPhotos(IDatabase database, GameClient session, int albumId)
    {
        using var connection = database.Connection();
        var access = await GetAlbumAccess(connection, albumId, session.GetHabbo().Id);
        if (!access.CanView)
            return;
        var photos = (await connection.QueryAsync<(int Id, string Url, int Timestamp, int OwnerId, string OwnerName)>(
            "SELECT cw.`id`, cw.`url`, cw.`timestamp`, cw.`user_id`, u.`username` FROM `camera_web_album_photos` ap " +
            "JOIN `camera_web` cw ON cw.`id` = ap.`photo_id` JOIN `users` u ON u.`id` = cw.`user_id` " +
            "WHERE ap.`album_id` = @albumId ORDER BY ap.`id` DESC",
            new { albumId })).ToList();
        session.Send(new RpAlbumPhotosComposer(albumId, photos
            .Select(photo => new RpAlbumPhotosComposer.Photo(photo.Id, photo.Url, photo.Timestamp, photo.OwnerId, photo.OwnerName))
            .ToList()));
    }
}
