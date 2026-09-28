using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

/// <summary>
/// pixelrp turfs: the turf panel's Claim button - the one way to claim a turf.
/// No payload: the turf is the room the player stands in, and every rule is
/// TurfManager.TryClaim's.
/// </summary>
internal class RpTurfClaimEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var room = session.GetHabbo()?.CurrentRoom;
        if (room != null)
            TurfManager.TryClaim(session, room);
        return Task.CompletedTask;
    }
}
