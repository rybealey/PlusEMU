using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Plus.Core.Settings;

namespace Plus.HabboHotel.Camera;

public class CameraPhotoManager : ICameraPhotoManager
{
    private const string DefaultStoragePath = "/camera-storage";
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<CameraPhotoManager> _logger;
    private readonly ConcurrentDictionary<int, byte[]> _pendingThumbnails = new();
    private readonly ConcurrentDictionary<int, PendingPhoto> _pendingPhotos = new();

    public CameraPhotoManager(ISettingsManager settingsManager, ILogger<CameraPhotoManager> logger)
    {
        _settingsManager = settingsManager;
        _logger = logger;
    }

    private string StoragePath => GetSettingOrDefault("camera.storage.path", DefaultStoragePath);

    private string UrlBase => GetSettingOrDefault("camera.url.base", "");

    private string GetSettingOrDefault(string key, string fallback)
    {
        var value = _settingsManager.TryGetValue(key);
        return string.IsNullOrEmpty(value) || value == "0" ? fallback : value;
    }

    public void StoreThumbnail(int userId, byte[] bytes) => _pendingThumbnails[userId] = bytes;

    public void StoreRoomThumbnail(uint roomId, byte[] bytes)
    {
        // Served at "{camera.url.base}/thumbnail/{roomId}.png", which is what
        // the client's thumbnails.url config resolves %thumbnail% (= roomId) to.
        var directory = Path.Combine(StoragePath, "thumbnail");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, $"{roomId}.png"), bytes);
    }

    public string StorePhoto(int userId, uint roomId, string roomName, byte[] bytes)
    {
        var urlBase = UrlBase;
        if (string.IsNullOrEmpty(urlBase))
        {
            // camera.url.base is unset (missing server_settings row). Writing
            // the file anyway would silently persist a broken root-relative
            // URL ("/photo_x.png") into items.extra_data and camera_web
            // forever, so fail loud instead — the packet handler's exception
            // path logs this and the client just doesn't get a success reply.
            _logger.LogError("camera.url.base is not configured; refusing to store photo for user {userId} to avoid persisting a broken URL", userId);
            throw new InvalidOperationException("camera.url.base setting is not configured; cannot store photo.");
        }

        Directory.CreateDirectory(StoragePath);
        var photoId = Guid.NewGuid().ToString("N");
        File.WriteAllBytes(Path.Combine(StoragePath, $"photo_{photoId}.png"), bytes);
        // The in-room furni visualization loads its texture from the "_small"
        // variant of the photo URL (FurnitureExternalImageVisualization replaces
        // ".png" with "_small.png"); without this file the wall item renders as a
        // black rectangle. Use the captured thumbnail when we have it, else the
        // full image, so the small variant always exists.
        var smallBytes = _pendingThumbnails.TryRemove(userId, out var thumb) ? thumb : bytes;
        File.WriteAllBytes(Path.Combine(StoragePath, $"photo_{photoId}_small.png"), smallBytes);
        var url = $"{urlBase}/photo_{photoId}.png";
        var pending = new PendingPhoto(photoId, roomId, roomName ?? "", url, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _pendingPhotos[userId] = pending;
        return url;
    }

    /// <summary>
    /// pixelrp: the file extension these bytes deserve, read from the bytes.
    ///
    /// The camera saves stills and, since the Video mode, animated GIFs, down
    /// the same packet. Nothing on the wire says which - and nothing should:
    /// a flag is a second source of truth that can disagree with the file it
    /// describes, where the magic number cannot. A GIF opens "GIF8"; anything
    /// else is written as the PNG this only ever used to produce.
    /// </summary>
    private static string ExtensionFor(byte[] bytes) =>
        (bytes != null && bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            ? "gif"
            : "png";

    public string StoreEditedPhoto(byte[] bytes)
    {
        var urlBase = UrlBase;
        if (string.IsNullOrEmpty(urlBase))
        {
            _logger.LogError("camera.url.base is not configured; refusing to store edited photo");
            throw new InvalidOperationException("camera.url.base setting is not configured; cannot store photo.");
        }

        Directory.CreateDirectory(StoragePath);
        var photoId = Guid.NewGuid().ToString("N");
        var ext = ExtensionFor(bytes);
        File.WriteAllBytes(Path.Combine(StoragePath, $"photo_{photoId}.{ext}"), bytes);
        // Same bytes for the _small variant — edits happen outside the
        // capture flow, so there's no separate thumbnail render to use. A GIF
        // gets a GIF thumbnail: its first frame is the still everything else
        // would have used, and decoding one here to flatten it would be work
        // for a file the wall-item path never asks of a camera photo.
        File.WriteAllBytes(Path.Combine(StoragePath, $"photo_{photoId}_small.{ext}"), bytes);
        return $"{urlBase}/photo_{photoId}.{ext}";
    }

    public bool TryGetPending(int userId, out PendingPhoto pending) => _pendingPhotos.TryGetValue(userId, out pending);

    public bool TryConsumePurchase(int userId, out PendingPhoto pending)
    {
        if (!_pendingPhotos.TryGetValue(userId, out pending))
            return false;

        lock (pending)
        {
            if (pending.Purchased)
                return false;
            pending.Purchased = true;
            return true;
        }
    }

    public void ResetPurchase(int userId)
    {
        if (!_pendingPhotos.TryGetValue(userId, out var pending))
            return;

        lock (pending)
            pending.Purchased = false;
    }

    public bool TryMarkPublished(int userId)
    {
        if (!_pendingPhotos.TryGetValue(userId, out var pending))
            return false;

        lock (pending)
        {
            if (pending.Published)
                return false;
            pending.Published = true;
            return true;
        }
    }
}
