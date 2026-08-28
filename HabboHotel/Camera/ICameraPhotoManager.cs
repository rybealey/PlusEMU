using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Camera;

[Singleton]
public interface ICameraPhotoManager
{
    void StoreThumbnail(int userId, byte[] bytes);

    /// <summary>
    ///     Persists a room's thumbnail image (set from the in-client room
    ///     thumbnail camera) under the camera storage path as
    ///     <c>thumbnail/{roomId}.png</c>, which is what the client's
    ///     <c>thumbnails.url</c> resolves to.
    /// </summary>
    void StoreRoomThumbnail(uint roomId, byte[] bytes);

    string StorePhoto(int userId, uint roomId, string roomName, byte[] bytes);

    /// <summary>
    ///     pixelrp: persists an edited (cropped/zoomed) copy of a photo as a
    ///     brand-new file pair (full + _small) and returns its absolute URL.
    ///     No pending-photo state involved; the caller updates the owning
    ///     camera_web row. The original file stays — photo furni printed
    ///     from it keep rendering.
    /// </summary>
    string StoreEditedPhoto(byte[] bytes);

    bool TryGetPending(int userId, out PendingPhoto pending);

    /// <summary>
    ///     Marks the user's pending photo as purchased. Returns true the first
    ///     time (caller should create the inventory item); returns false and
    ///     leaves <paramref name="pending" /> null when there is no pending
    ///     photo at all, or returns false with <paramref name="pending" />
    ///     populated when this photo was already purchased (caller should
    ///     still reply OK but skip creating a second item).
    /// </summary>
    bool TryConsumePurchase(int userId, out PendingPhoto pending);

    /// <summary>
    ///     Rolls back a purchase reservation made by
    ///     <see cref="TryConsumePurchase" /> when the caller could not actually
    ///     create the inventory item, so a later retry can genuinely re-attempt
    ///     instead of hitting the "already purchased" short-circuit.
    /// </summary>
    void ResetPurchase(int userId);

    /// <summary>
    ///     Marks the user's pending photo as published. Returns true only the
    ///     first time (caller should insert the camera_web row); returns
    ///     false if there is no pending photo, or if it was already published
    ///     (caller should still reply success but skip a second insert).
    /// </summary>
    bool TryMarkPublished(int userId);
}
