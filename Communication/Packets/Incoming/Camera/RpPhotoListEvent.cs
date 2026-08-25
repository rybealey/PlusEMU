using Dapper;
using Plus.Communication.Packets.Outgoing.Camera;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: the phone's Photos app asked for the player's photo library.
/// Replies with their camera_web rows (private saves and published shots
/// alike), newest first.
/// </summary>
internal class RpPhotoListEvent : IPacketEvent
{
    private const int MaxPhotos = 120;

    private readonly IDatabase _database;

    public RpPhotoListEvent(IDatabase database)
    {
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        using var connection = _database.Connection();
        var rows = await connection.QueryAsync<(int Id, string Url, int Timestamp, bool Visible)>(
            "SELECT `id`, `url`, `timestamp`, `visible` FROM `camera_web` WHERE `user_id` = @userId ORDER BY `id` DESC LIMIT @limit",
            new { userId = habbo.Id, limit = MaxPhotos });
        session.Send(new RpPhotoListComposer(rows.Select(row => new RpPhotoListComposer.Photo(row.Id, row.Url, row.Timestamp, row.Visible)).ToList()));
    }
}
