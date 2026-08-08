using Plus.Communication.Packets.Outgoing.Camera;
using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

internal class RenderRoomThumbnailEvent : IPacketEvent
{
    // Client-supplied length guards allocation from a hostile/buggy client
    // sending a huge or negative value. Unlike RenderRoom, this protocol has
    // a failure variant, so an out-of-range length replies with
    // ThumbnailStatusMessageComposer(false) instead of a silent return — the
    // bytes are never read off the wire on the reject path.
    private const int MaxThumbnailBytes = 512_000;

    private readonly ICameraPhotoManager _cameraPhotoManager;

    public RenderRoomThumbnailEvent(ICameraPhotoManager cameraPhotoManager)
    {
        _cameraPhotoManager = cameraPhotoManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var length = packet.ReadInt();
        if (length <= 0 || length > MaxThumbnailBytes)
        {
            session.Send(new ThumbnailStatusMessageComposer(false));
            return Task.CompletedTask;
        }

        var bytes = new byte[length];
        packet.ReadBytes(bytes);
        _cameraPhotoManager.StoreThumbnail(session.GetHabbo().Id, bytes);
        session.Send(new ThumbnailStatusMessageComposer(true));
        return Task.CompletedTask;
    }
}
