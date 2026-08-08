using Plus.Communication.Packets.Outgoing.Camera;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

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

        var url = _cameraPhotoManager.StorePhoto(session.GetHabbo().Id, room.RoomId, bytes);
        session.Send(new CameraStorageUrlMessageComposer(url));
        return Task.CompletedTask;
    }
}
