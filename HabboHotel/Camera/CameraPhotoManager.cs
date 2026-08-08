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

    public string StorePhoto(int userId, uint roomId, byte[] bytes)
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
        if (_pendingThumbnails.TryRemove(userId, out var thumb))
            File.WriteAllBytes(Path.Combine(StoragePath, $"thumb_{photoId}.png"), thumb);
        var url = $"{urlBase}/photo_{photoId}.png";
        var pending = new PendingPhoto(photoId, roomId, url, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _pendingPhotos[userId] = pending;
        return url;
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
