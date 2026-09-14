using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Incoming.Users.Sitch;

/// <summary>
/// pixelrp: Sitch search. A query starting with # searches the tag index; any
/// other query searches names and bodies.
/// </summary>
internal class RpSitchSearchEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var query = (packet.ReadString() ?? "").Trim();
        if (session.GetHabbo() == null) return Task.CompletedTask;

        // An empty query is a cleared box, not a request for everything.
        if (query.Length == 0) return Task.CompletedTask;

        SitchUtility.SendSearch(session, query);
        return Task.CompletedTask;
    }
}
