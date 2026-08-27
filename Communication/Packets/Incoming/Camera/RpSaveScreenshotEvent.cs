using Dapper;
using Plus.Communication.Attributes;
using Plus.Database;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: the phone's side button captured a screenshot of the phone
/// screen. Store it like an edited photo (new file pair, no pending-photo
/// state, no furni) and file it straight into the player's private photo
/// library. Works anywhere — no room required. Replies with the refreshed
/// library.
/// </summary>
[VipOnly]
internal class RpSaveScreenshotEvent : IPacketEvent
{
    private const int MaxPhotoBytes = 2_000_000;

    private readonly ICameraPhotoManager _cameraPhotoManager;
    private readonly IDatabase _database;

    public RpSaveScreenshotEvent(ICameraPhotoManager cameraPhotoManager, IDatabase database)
    {
        _cameraPhotoManager = cameraPhotoManager;
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var length = packet.ReadInt();
        if (length <= 0 || length > MaxPhotoBytes)
            return;
        var bytes = new byte[length];
        packet.ReadBytes(bytes);

        var habbo = session.GetHabbo();
        if (habbo == null)
            return;

        var url = _cameraPhotoManager.StoreEditedPhoto(bytes);
        using (var connection = _database.Connection())
        {
            await connection.ExecuteAsync(
                "INSERT INTO `camera_web` (`user_id`, `room_id`, `timestamp`, `url`, `visible`) VALUES (@userId, @roomId, @timestamp, @url, 0)",
                new { userId = habbo.Id, roomId = (habbo.CurrentRoom?.RoomId ?? 0), timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), url });
        }
        await RpPhotoLibrary.SendPhotoList(_database, session);
    }
}
