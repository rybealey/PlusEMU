using System.Collections.Concurrent;
using Plus.Core.Settings;

namespace Plus.HabboHotel.Camera;

public class CameraPhotoManager : ICameraPhotoManager
{
    private const string DefaultStoragePath = "/camera-storage";
    private readonly ISettingsManager _settingsManager;
    private readonly ConcurrentDictionary<int, byte[]> _pendingThumbnails = new();
    private readonly ConcurrentDictionary<int, PendingPhoto> _pendingPhotos = new();

    public CameraPhotoManager(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    private string StoragePath
    {
        get
        {
            var path = _settingsManager.TryGetValue("camera.storage.path");
            return string.IsNullOrEmpty(path) ? DefaultStoragePath : path;
        }
    }

    private string UrlBase => _settingsManager.TryGetValue("camera.url.base") ?? "";

    public void StoreThumbnail(int userId, byte[] bytes) => _pendingThumbnails[userId] = bytes;

    public string StorePhoto(int userId, uint roomId, byte[] bytes)
    {
        Directory.CreateDirectory(StoragePath);
        var photoId = Guid.NewGuid().ToString("N");
        File.WriteAllBytes(Path.Combine(StoragePath, $"photo_{photoId}.png"), bytes);
        if (_pendingThumbnails.TryRemove(userId, out var thumb))
            File.WriteAllBytes(Path.Combine(StoragePath, $"thumb_{photoId}.png"), thumb);
        var url = $"{UrlBase}/photo_{photoId}.png";
        var pending = new PendingPhoto(photoId, roomId, url, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _pendingPhotos[userId] = pending;
        return url;
    }

    public bool TryGetPending(int userId, out PendingPhoto pending) => _pendingPhotos.TryGetValue(userId, out pending);
}
