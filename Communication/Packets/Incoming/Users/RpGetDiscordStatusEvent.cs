using Plus.HabboHotel.Discord;
using Plus.HabboHotel.GameClients;
using Plus.Communication.Packets.Outgoing.Users;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the Settings Discord page opened - report link status.
/// </summary>
internal class RpGetDiscordStatusEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null)
            return Task.CompletedTask;
        session.Send(new RpDiscordStatusComposer(DiscordSyncUtility.IsLinked(session.GetHabbo().Id)));
        return Task.CompletedTask;
    }
}
