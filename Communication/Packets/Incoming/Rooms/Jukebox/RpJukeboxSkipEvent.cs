using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Jukebox;

// PixelRP: client skips the currently playing jukebox track. No payload;
// rights check lives in RoomJukeboxManager.TrySkip.
internal class RpJukeboxSkipEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        session.GetHabbo()?.CurrentRoom?.GetJukeboxManager().TrySkip(session);
        return Task.CompletedTask;
    }
}
