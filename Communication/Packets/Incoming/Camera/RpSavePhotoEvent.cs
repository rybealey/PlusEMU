using Dapper;
using Plus.Communication.Attributes;
using Plus.Database;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: the phone camera captured a room shot. Unlike the classic camera
/// (RenderRoom + PurchasePhoto), phone photos never become inventory furni -
/// this stores the image and files it straight into the player's private
/// photo library in one step, with metadata: source 'camera', a snapshot of
/// the room's name, and the players the client says were inside the frame.
/// The claimed player list is validated against the room's live roster (and
/// usernames are resolved server-side), so nobody can tag a player who
/// wasn't actually in the room. Replies with the refreshed library.
/// </summary>
[VipOnly]
internal class RpSavePhotoEvent : IPacketEvent
{
    private const int MaxPhotoBytes = 2_000_000;
    private const int MaxTaggedUsers = 25;

    private readonly ICameraPhotoManager _cameraPhotoManager;
    private readonly IDatabase _database;

    public RpSavePhotoEvent(ICameraPhotoManager cameraPhotoManager, IDatabase database)
    {
        _cameraPhotoManager = cameraPhotoManager;
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userCount = packet.ReadInt();
        if (userCount < 0 || userCount > MaxTaggedUsers)
            return;
        var claimedIds = new List<int>(userCount);
        for (var i = 0; i < userCount; i++)
            claimedIds.Add(packet.ReadInt());

        var length = packet.ReadInt();
        if (length <= 0 || length > MaxPhotoBytes)
            return;
        var bytes = new byte[length];
        packet.ReadBytes(bytes);

        var habbo = session.GetHabbo();
        var room = habbo?.CurrentRoom;
        if (habbo == null || room == null)
            return;

        // Keep only claimed ids that are really in the room right now, and
        // take their usernames from the roster - never from the client.
        var taggedUsers = new List<(int UserId, string Username)>();
        foreach (var claimedId in claimedIds.Distinct())
        {
            var roomUser = room.GetRoomUserManager().GetRoomUserByHabbo(claimedId);
            if (roomUser == null || roomUser.IsBot)
                continue;
            taggedUsers.Add((claimedId, roomUser.GetUsername()));
        }

        var url = _cameraPhotoManager.StoreEditedPhoto(bytes);
        using (var connection = _database.Connection())
        {
            await connection.ExecuteAsync(
                "INSERT INTO `camera_web` (`user_id`, `room_id`, `room_name`, `timestamp`, `url`, `visible`, `source`) VALUES (@userId, @roomId, @roomName, @timestamp, @url, 0, 'camera')",
                new { userId = habbo.Id, roomId = room.RoomId, roomName = room.Name ?? "", timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), url });

            if (taggedUsers.Count > 0)
            {
                // The photo's fresh row id - the url embeds a per-photo GUID,
                // so this lookup is unambiguous (and avoids relying on
                // multi-statement LAST_INSERT_ID support).
                var photoId = await connection.ExecuteScalarAsync<long>(
                    "SELECT `id` FROM `camera_web` WHERE `user_id` = @userId AND `url` = @url ORDER BY `id` DESC LIMIT 1",
                    new { userId = habbo.Id, url });
                if (photoId > 0)
                    await connection.ExecuteAsync(
                        "INSERT INTO `camera_web_users` (`photo_id`, `user_id`, `username`) VALUES (@photoId, @userId, @username)",
                        taggedUsers.Select(user => new { photoId, userId = user.UserId, username = user.Username }));
            }
        }
        await RpPhotoLibrary.SendPhotoList(_database, session);
    }
}
