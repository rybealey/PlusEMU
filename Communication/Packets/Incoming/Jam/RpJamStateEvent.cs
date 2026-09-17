using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Jam;

namespace Plus.Communication.Packets.Incoming.Jam;

// PixelRP: "what jam am I in?" - asked by the client at startup and whenever it
// reconnects. The answer may well be "none", which is a real answer: a freshly
// loaded client that has heard nothing cannot tell not-in-a-jam apart from
// not-told-yet, and the difference decides whether it plays anything.
internal class RpJamStateEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        JamManager.SendState(session);
        return Task.CompletedTask;
    }
}
