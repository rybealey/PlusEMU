using Dapper;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: the phone's Photos app deleted a photo. Removes the player's
/// own camera_web row (published rows disappear from the CMS feed too) and
/// replies with the refreshed library. The image file stays on disk —
/// photo furni printed from it keep rendering.
/// </summary>
internal class RpDeletePhotoEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpDeletePhotoEvent(IDatabase database)
    {
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var photoId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        using (var connection = _database.Connection())
        {
            var deleted = await connection.ExecuteAsync(
                "DELETE FROM `camera_web` WHERE `id` = @photoId AND `user_id` = @userId",
                new { photoId, userId = habbo.Id });
            // Tagged-player rows go with the photo (ownership was just
            // verified by the delete above actually removing a row).
            if (deleted > 0)
                await connection.ExecuteAsync(
                    "DELETE FROM `camera_web_users` WHERE `photo_id` = @photoId",
                    new { photoId });
        }
        await RpPhotoLibrary.SendPhotoList(_database, session);
    }
}
