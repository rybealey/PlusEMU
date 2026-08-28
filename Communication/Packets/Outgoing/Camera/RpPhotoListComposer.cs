using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Camera;

/// <summary>
/// pixelrp: the player's photo library for the phone's Photos app — every
/// camera_web row they own, newest first. Absolute URLs; published marks
/// photos that are visible on the CMS Photos page.
/// </summary>
public class RpPhotoListComposer : IServerPacket
{
    // Source: 'camera' | 'screenshot' | 'saved' | '' (legacy rows). RoomName
    // is the room's name snapshotted at capture time; TaggedUsers the players
    // who were inside a phone camera shot's frame (empty otherwise).
    public record Photo(int Id, string Url, int Timestamp, bool Published, string Source, string RoomName, List<string> TaggedUsers);

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
            packet.WriteString(photo.Source);
            packet.WriteString(photo.RoomName);
            packet.WriteInteger(photo.TaggedUsers.Count);
            foreach (var username in photo.TaggedUsers)
                packet.WriteString(username);
        }
    }
}
