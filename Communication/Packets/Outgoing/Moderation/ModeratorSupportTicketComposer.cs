using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.Utilities;

namespace Plus.Communication.Packets.Outgoing.Moderation;

public class ModeratorSupportTicketComposer : IServerPacket
{
    private readonly int _id;
    private readonly ModerationTicket _ticket;
    public uint MessageId => ServerPacketHeader.ModeratorSupportTicketComposer;

    public ModeratorSupportTicketComposer(int id, ModerationTicket ticket)
    {
        _id = id;
        _ticket = ticket;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_ticket.Id); // Id
        packet.WriteInteger(_ticket.GetStatus(_id)); // Tab ID
        packet.WriteInteger(_ticket.Type); // Type
        packet.WriteInteger(_ticket.Category); // Category
        packet.WriteInteger(Convert.ToInt32((DateTime.Now - UnixTimestamp.FromUnixTimestamp(_ticket.Timestamp)).TotalMilliseconds)); // This should fix the overflow?
        packet.WriteInteger(_ticket.Priority); // Priority
        packet.WriteInteger(0); //??
        packet.WriteInteger(_ticket.SenderId); // Sender ID
        //base.WriteInteger(1);
        packet.WriteString(_ticket.SenderUsername); // Sender Name
        packet.WriteInteger(_ticket.ReportedId); // Reported ID
        packet.WriteString(_ticket.ReportedUsername); // Reported Name
        packet.WriteInteger(_ticket.ModeratorId); // Moderator ID
        packet.WriteString(_ticket.ModeratorUsername); // Mod Name
        packet.WriteString(_ticket.Issue); // Issue
        packet.WriteUInteger(_ticket.RoomId); // Room Id
        packet.WriteInteger(0);
        {
            // push String
            // push Integer
            // push Integer
        }
    }
}