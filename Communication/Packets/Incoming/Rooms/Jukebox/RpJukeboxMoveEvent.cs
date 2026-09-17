using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Jukebox;

// PixelRP: a song is dragged up or down THIS ROOM's queue.
//
// Routed by the room the player is standing in, exactly as adding is: a station
// belongs to a room, and which room they are in is the whole of the routing.
// The rights check lives in JukeboxStation.TryMove, next to the queue it
// guards.
internal class RpJukeboxMoveEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var from = packet.ReadInt();
        var to = packet.ReadInt();
        if (session.GetHabbo() == null)
            return Task.CompletedTask;
        session.GetHabbo().CurrentRoom?.GetJukeboxManager()?.TryMove(session, from, to);
        return Task.CompletedTask;
    }
}
