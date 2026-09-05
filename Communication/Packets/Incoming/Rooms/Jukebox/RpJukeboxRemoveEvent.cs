using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Jukebox;

namespace Plus.Communication.Packets.Incoming.Rooms.Jukebox;

// PixelRP: client removes a queued jukebox track by index (own track, or
// room rights). Rights/ownership checks live in RoomJukeboxManager.TryRemove.
internal class RpJukeboxRemoveEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var index = packet.ReadInt();
        if (session.GetHabbo() != null) JukeboxStation.TryRemove(session, index);
        return Task.CompletedTask;
    }
}
