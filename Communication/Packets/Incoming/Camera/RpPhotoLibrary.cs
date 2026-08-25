using Dapper;
using Plus.Communication.Packets.Outgoing.Camera;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: shared reply for the phone's Photos app — the session user's
/// camera_web rows, newest first. Sent after list requests, deletes and
/// edits so the client's library state always comes from one source.
/// </summary>
internal static class RpPhotoLibrary
{
    private const int MaxPhotos = 120;

    public static async Task SendPhotoList(IDatabase database, GameClient session)
    {
        using var connection = database.Connection();
        var rows = await connection.QueryAsync<(int Id, string Url, int Timestamp, bool Visible)>(
            "SELECT `id`, `url`, `timestamp`, `visible` FROM `camera_web` WHERE `user_id` = @userId ORDER BY `id` DESC LIMIT @limit",
            new { userId = session.GetHabbo().Id, limit = MaxPhotos });
        session.Send(new RpPhotoListComposer(rows.Select(row => new RpPhotoListComposer.Photo(row.Id, row.Url, row.Timestamp, row.Visible)).ToList()));
    }
}
