using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: the phone's Photos app opened its Collections tab - reply with
/// every album the player owns or is a member of.
/// </summary>
internal class RpRequestAlbumsEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpRequestAlbumsEvent(IDatabase database)
    {
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null)
            return;
        await RpAlbumLibrary.SendAlbumList(_database, session);
    }
}
