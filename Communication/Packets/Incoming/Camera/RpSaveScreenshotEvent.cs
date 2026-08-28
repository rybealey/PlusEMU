using Dapper;
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
        // Leading kind flag: 0 = a phone-screen screenshot, 1 = a photo
        // received in a DM and saved into the library ("saved"). Anything
        // else is rejected.
        var kind = packet.ReadInt();
        if (kind != 0 && kind != 1)
            return;
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
                "INSERT INTO `camera_web` (`user_id`, `room_id`, `room_name`, `timestamp`, `url`, `visible`, `source`) VALUES (@userId, @roomId, @roomName, @timestamp, @url, 0, @source)",
                new
                {
                    userId = habbo.Id,
                    roomId = (habbo.CurrentRoom?.RoomId ?? 0),
                    roomName = (habbo.CurrentRoom?.Name ?? ""),
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    url,
                    source = (kind == 1 ? "saved" : "screenshot")
                });
        }
        await RpPhotoLibrary.SendPhotoList(_database, session);
    }
}
