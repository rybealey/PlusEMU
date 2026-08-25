using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Camera;

/// <summary>
/// pixelrp: the player's photo library for the phone's Photos app — every
/// camera_web row they own, newest first. Absolute URLs; published marks
/// photos that are visible on the CMS Photos page.
/// </summary>
public class RpPhotoListComposer : IServerPacket
{
    public record Photo(int Id, string Url, int Timestamp, bool Published);

    private readonly List<Photo> _photos;

    public uint MessageId => ServerPacketHeader.RpPhotoListComposer;

    public RpPhotoListComposer(List<Photo> photos)
    {
        _photos = photos;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_photos.Count);
        foreach (var photo in _photos)
        {
            packet.WriteInteger(photo.Id);
            packet.WriteString(photo.Url);
            packet.WriteInteger(photo.Timestamp);
            packet.WriteBoolean(photo.Published);
        }
    }
}
