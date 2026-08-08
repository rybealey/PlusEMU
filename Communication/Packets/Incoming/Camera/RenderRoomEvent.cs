using Plus.Communication.Packets.Outgoing.Camera;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

internal class RenderRoomEvent : IPacketEvent
{
    private readonly ICameraPhotoManager _cameraPhotoManager;

    public RenderRoomEvent(ICameraPhotoManager cameraPhotoManager)
    {
        _cameraPhotoManager = cameraPhotoManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var length = packet.ReadInt();
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
