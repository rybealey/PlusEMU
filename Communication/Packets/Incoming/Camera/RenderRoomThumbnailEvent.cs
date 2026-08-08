using Plus.Communication.Packets.Outgoing.Camera;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

internal class RenderRoomThumbnailEvent : IPacketEvent
{
    private readonly ICameraPhotoManager _cameraPhotoManager;

    public RenderRoomThumbnailEvent(ICameraPhotoManager cameraPhotoManager)
    {
        _cameraPhotoManager = cameraPhotoManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var length = packet.ReadInt();
        var bytes = new byte[length];
        packet.ReadBytes(bytes);
        _cameraPhotoManager.StoreThumbnail(session.GetHabbo().Id, bytes);
        session.Send(new ThumbnailStatusMessageComposer(true));
        return Task.CompletedTask;
    }
}
