using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// pixelrp: the phone's Photos app asked for the player's photo library.
/// Replies with their camera_web rows (private saves and published shots
/// alike), newest first.
/// </summary>
internal class RpPhotoListEvent : IPacketEvent
{
    private readonly IDatabase _database;

    public RpPhotoListEvent(IDatabase database)
    {
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null)
            return;
        await RpPhotoLibrary.SendPhotoList(_database, session);
    }
}
