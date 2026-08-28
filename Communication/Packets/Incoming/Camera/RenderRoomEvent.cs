using Plus.Communication.Attributes;
using Plus.Communication.Packets.Outgoing.Camera;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

[VipOnly]
internal class RenderRoomEvent : IPacketEvent
{
    // Client-supplied length guards allocation from a hostile/buggy client
    // sending a huge or negative value. RenderRoom's protocol has no failure
    // variant, so an out-of-range length is a silent return (same agreed
    // pattern as PurchasePhoto's missing-pending case) — no reply, and the
    // bytes are never read off the wire.
    private const int MaxPhotoBytes = 2_000_000;

    private readonly ICameraPhotoManager _cameraPhotoManager;

    public RenderRoomEvent(ICameraPhotoManager cameraPhotoManager)
    {
        _cameraPhotoManager = cameraPhotoManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var length = packet.ReadInt();
        if (length <= 0 || length > MaxPhotoBytes)
            return Task.CompletedTask;

        var bytes = new byte[length];
        packet.ReadBytes(bytes);

        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return Task.CompletedTask;

        _cameraPhotoManager.StorePhoto(session.GetHabbo().Id, room.RoomId, room.Name, bytes);

        // CameraStorageUrl replies with the filename only, not the absolute
        // URL — the client builds its checkout preview by concatenating the
        // ui-config "camera.url" base with this value. PendingPhoto.Url
        // stays absolute; PurchasePhoto's extradata "w" and PublishPhoto's
        // camera_web insert both need the absolute form and are unaffected.
        if (_cameraPhotoManager.TryGetPending(session.GetHabbo().Id, out var pending))
            session.Send(new CameraStorageUrlMessageComposer($"photo_{pending.PhotoId}.png"));
        return Task.CompletedTask;
    }
}
