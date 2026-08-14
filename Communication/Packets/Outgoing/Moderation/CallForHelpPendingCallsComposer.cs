using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.Utilities;

namespace Plus.Communication.Packets.Outgoing.Moderation;

public class CallForHelpPendingCallsComposer : IServerPacket
{
    private readonly ModerationTicket _ticket;

    // This IS the client's CFH_PENDING_CALLS packet — the same one OpenHelpToolComposer
    // sends with an empty list, which is why ServerPacketHeader has it twice. Its own
    // constant is 0, and headers of 0 are dropped from the revision's outgoing map, so
    // sending it threw KeyNotFoundException out of the packet handler instead of telling
    // the reporter they already have a call open. Use the header that is actually mapped.
    public uint MessageId => ServerPacketHeader.OpenHelpToolComposer;

    public CallForHelpPendingCallsComposer(ModerationTicket ticket)
    {
        _ticket = ticket;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(1); // Count for whatever reason?
        {
            packet.WriteString(_ticket.Id.ToString());
            packet.WriteString(UnixTimestamp.FromUnixTimestamp(_ticket.Timestamp).ToShortTimeString()); // "11-02-2017 04:07:05";
            packet.WriteString(_ticket.Issue);
        }
    }
}