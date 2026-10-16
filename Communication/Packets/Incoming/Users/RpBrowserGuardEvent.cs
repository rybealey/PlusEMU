using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Security;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the browser guard sending this player back to the website - why
/// (BrowserGuardLog.Reason*) and a line of detail (which shortcut, which
/// watch, the browser). Sent on the way out, just before the client closes
/// the connection. Written down in BrowserGuardLog.
/// </summary>
internal class RpBrowserGuardEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var reason = packet.ReadInt();
        var detail = packet.ReadString();
        if (session.GetHabbo() != null)
            BrowserGuardLog.Record(session, reason, detail);
        return Task.CompletedTask;
    }
}
