using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Camera;

[Singleton]
public interface ICameraPhotoManager
{
    void StoreThumbnail(int userId, byte[] bytes);
    string StorePhoto(int userId, uint roomId, byte[] bytes);
    bool TryGetPending(int userId, out PendingPhoto pending);
}
