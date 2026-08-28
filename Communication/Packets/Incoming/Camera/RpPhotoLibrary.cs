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
        var rows = (await connection.QueryAsync<(int Id, string Url, int Timestamp, bool Visible, string Source, string RoomName)>(
            "SELECT `id`, `url`, `timestamp`, `visible`, `source`, `room_name` FROM `camera_web` WHERE `user_id` = @userId ORDER BY `id` DESC LIMIT @limit",
            new { userId = session.GetHabbo().Id, limit = MaxPhotos })).ToList();

        // Tagged players per photo (phone camera shots only; empty for the
        // rest) - one query for the whole page of photos.
        var taggedByPhoto = new Dictionary<int, List<string>>();
        if (rows.Count > 0)
        {
            var tagRows = await connection.QueryAsync<(int PhotoId, string Username)>(
                "SELECT `photo_id`, `username` FROM `camera_web_users` WHERE `photo_id` IN @photoIds",
                new { photoIds = rows.Select(row => row.Id).ToList() });
            foreach (var tag in tagRows)
            {
                if (!taggedByPhoto.TryGetValue(tag.PhotoId, out var names))
                    taggedByPhoto[tag.PhotoId] = names = new List<string>();
                names.Add(tag.Username);
            }
        }

        session.Send(new RpPhotoListComposer(rows
            .Select(row => new RpPhotoListComposer.Photo(row.Id, row.Url, row.Timestamp, row.Visible, row.Source ?? "", row.RoomName ?? "",
                taggedByPhoto.TryGetValue(row.Id, out var names) ? names : new List<string>()))
            .ToList()));
    }
}
