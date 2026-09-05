using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Jukebox;

namespace Plus.Communication.Packets.Incoming.Rooms.Jukebox;

// PixelRP: client skips the currently playing jukebox track. No payload;
// rights check lives in RoomJukeboxManager.TrySkip.
internal class RpJukeboxSkipEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() != null) JukeboxStation.TrySkip(session);
        return Task.CompletedTask;
    }
}
