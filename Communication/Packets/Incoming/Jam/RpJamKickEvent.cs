using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: the host puts somebody out of their jam.
//
// Nothing is announced. Not to the jam, not to the room, not to the person -
// their music stops and the app shows them no jam, which is answer enough. A
// kick with a broadcast attached is a scene; this is the quiet end of somebody's
// evening in here.
//
// The host's alone, and never on themselves - ending the jam is a different
// button. Both checks live in JamSession.TryKick, where the member list is.
internal class RpJamKickEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var targetId = packet.ReadInt();
        JamManager.Kick(session.GetHabbo(), targetId);
        return Task.CompletedTask;
    }
}
