namespace Plus.HabboHotel.Camera;

/// <summary>
///     The most recently rendered photo for a user, pending purchase and/or
///     publish. Purchase and publish are independent one-shot actions per
///     photo: <see cref="Purchased" /> and <see cref="Published" /> are each
///     flipped at most once (guarded by <see cref="CameraPhotoManager" />
///     locking on this instance), so a duplicate click/retry no-ops instead
///     of creating a second inventory item or a second camera_web row.
/// </summary>
public class PendingPhoto
{
    public string PhotoId { get; }
    public uint RoomId { get; }

    // pixelrp: the room's name snapshotted at render time - stored on the
    // camera_web row so the caption survives later room renames/deletions.
    public string RoomName { get; }
    public string Url { get; }
    public long TakenUnixMs { get; }
    public bool Purchased { get; set; }
    public bool Published { get; set; }

    public PendingPhoto(string photoId, uint roomId, string roomName, string url, long takenUnixMs)
    {
        PhotoId = photoId;
        RoomId = roomId;
        RoomName = roomName;
        Url = url;
        TakenUnixMs = takenUnixMs;
    }
}
