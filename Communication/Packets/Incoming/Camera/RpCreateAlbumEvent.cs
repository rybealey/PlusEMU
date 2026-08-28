using Dapper;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: create a photo album from the phone's Photos app. Shared albums
/// carry an initial member list - every claimed member must be a friend of
/// the creator (anything else is silently dropped). Replies with the
/// refreshed album list.
/// </summary>
internal class RpCreateAlbumEvent : IPacketEvent
{
    private const int MaxNameLength = 32;
    private const int MaxMembers = 30;
    private const int MaxAlbumsPerUser = 50;

    private readonly IDatabase _database;

    public RpCreateAlbumEvent(IDatabase database)
    {
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var name = (packet.ReadString() ?? "").Trim();
        var shared = packet.ReadBool();
        var memberCount = packet.ReadInt();
        if (memberCount < 0 || memberCount > MaxMembers)
            return;
        var claimedMemberIds = new List<int>(memberCount);
        for (var i = 0; i < memberCount; i++)
            claimedMemberIds.Add(packet.ReadInt());

        var habbo = session.GetHabbo();
        if (habbo == null || name.Length < 1 || name.Length > MaxNameLength)
            return;

        // Members only make sense on shared albums, and only friends of the
        // creator can be added.
        var memberIds = (shared
            ? claimedMemberIds.Distinct().Where(id => (id != habbo.Id) && (habbo.Messenger.GetFriend(id) != null)).ToList()
            : new List<int>());

        using (var connection = _database.Connection())
        {
            var albumCount = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM `camera_web_albums` WHERE `owner_id` = @userId",
                new { userId = habbo.Id });
            if (albumCount >= MaxAlbumsPerUser)
                return;

            await connection.ExecuteAsync(
                "INSERT INTO `camera_web_albums` (`owner_id`, `name`, `is_shared`, `created_at`) VALUES (@userId, @name, @shared, @createdAt)",
                new { userId = habbo.Id, name, shared, createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            if (memberIds.Count > 0)
            {
                var albumId = await connection.ExecuteScalarAsync<long>(
                    "SELECT `id` FROM `camera_web_albums` WHERE `owner_id` = @userId ORDER BY `id` DESC LIMIT 1",
                    new { userId = habbo.Id });
                await connection.ExecuteAsync(
                    "INSERT IGNORE INTO `camera_web_album_members` (`album_id`, `user_id`) VALUES (@albumId, @memberId)",
                    memberIds.Select(memberId => new { albumId, memberId }));
            }
        }
        await RpAlbumLibrary.SendAlbumList(_database, session);
    }
}
