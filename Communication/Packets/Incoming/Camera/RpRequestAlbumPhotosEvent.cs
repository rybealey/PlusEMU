using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: the phone opened an album - reply with its photos (access
/// checked inside the shared helper).
/// </summary>
internal class RpRequestAlbumPhotosEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpRequestAlbumPhotosEvent(IDatabase database)
    {
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var albumId = packet.ReadInt();
        if (session.GetHabbo() == null)
            return;
        await RpAlbumLibrary.SendAlbumPhotos(_database, session, albumId);
    }
}
