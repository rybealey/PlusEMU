using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Support;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: the Support app asks for its state - the list, and one thread's messages when a thread is open.</summary>
internal class RpSupportOpenEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var threadId = packet.ReadInt();
        SupportUtility.SendView(session, threadId);
        return Task.CompletedTask;
    }
}
