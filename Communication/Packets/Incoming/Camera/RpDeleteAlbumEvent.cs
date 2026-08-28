using Dapper;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: delete a photo album (owner only). The photos themselves stay
/// in their owners' libraries - only the album, its memberships and its
/// photo links go. Replies with the refreshed album list.
/// </summary>
internal class RpDeleteAlbumEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpDeleteAlbumEvent(IDatabase database)
    {
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var albumId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        using (var connection = _database.Connection())
        {
            var deleted = await connection.ExecuteAsync(
                "DELETE FROM `camera_web_albums` WHERE `id` = @albumId AND `owner_id` = @userId",
                new { albumId, userId = habbo.Id });
            if (deleted > 0)
            {
                await connection.ExecuteAsync("DELETE FROM `camera_web_album_members` WHERE `album_id` = @albumId", new { albumId });
                await connection.ExecuteAsync("DELETE FROM `camera_web_album_photos` WHERE `album_id` = @albumId", new { albumId });
            }
        }
        await RpAlbumLibrary.SendAlbumList(_database, session);
    }
}
