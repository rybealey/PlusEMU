using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Apartments;

/// <summary>
/// pixelrp: the picture an owner's client takes of their apartment as they
/// walk out of it (RpApartments.LeaveSoon asks for it), for the real estate
/// board - a PNG, length first, as a room thumbnail is sent. Stored as that
/// apartment's room thumbnail (CameraPhotoManager.StoreRoomThumbnail), though
/// they may be on their way out of it by now. Taken only when the server just
/// asked this player for one (RpApartments.TakeSnapshotRoom); anything else is
/// dropped without being read. Not VIP-only like the thumbnail camera:
/// apartments are for every player.
/// </summary>
internal class RpApartmentSnapshotEvent : IPacketEvent
{
    // The thumbnail camera's own limit (RenderRoomThumbnailEvent).
    private const int MaxSnapshotBytes = 512_000;

    private readonly ICameraPhotoManager _cameraPhotoManager;

    public RpApartmentSnapshotEvent(ICameraPhotoManager cameraPhotoManager)
    {
        _cameraPhotoManager = cameraPhotoManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session?.GetHabbo() == null || packet.Buffer.Length < sizeof(int))
            return Task.CompletedTask;
        if (!RpApartments.TakeSnapshotRoom(session.GetHabbo().Id, out var roomId))
            return Task.CompletedTask;

        var length = packet.ReadInt();
        if (length <= 0 || length > MaxSnapshotBytes || length > packet.Buffer.Length - sizeof(int))
            return Task.CompletedTask;

        var bytes = new byte[length];
        packet.ReadBytes(bytes);
        _cameraPhotoManager.StoreRoomThumbnail(roomId, bytes);
        return Task.CompletedTask;
    }
}
