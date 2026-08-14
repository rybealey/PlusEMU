using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.Utilities;

namespace Plus.Communication.Packets.Outgoing.Moderation;

public class ModeratorInitComposer : IServerPacket
{
    private readonly ICollection<string> _userPresets;
    private readonly ICollection<string> _roomPresets;
    private readonly ICollection<ModerationTicket> _tickets;

    // Which tab a ticket belongs in is relative to who is looking: a ticket
    // picked by *this* moderator is "mine", anyone else's is not.
    private readonly int _viewerId;

    public uint MessageId => ServerPacketHeader.ModeratorInitComposer;

    public ModeratorInitComposer(int viewerId, ICollection<string> userPresets, ICollection<string> roomPresets, ICollection<ModerationTicket> tickets)
    {
        _viewerId = viewerId;
        _userPresets = userPresets;
        _roomPresets = roomPresets;
        _tickets = tickets;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_tickets.Count);
        foreach (var ticket in _tickets)
        {
            packet.WriteInteger(ticket.Id); // Id
            packet.WriteInteger(ticket.GetStatus(_viewerId)); // Tab ID
            packet.WriteInteger(ticket.Type); // Type
            packet.WriteInteger(ticket.Category); // Category
            packet.WriteInteger(Convert.ToInt32((DateTime.Now - UnixTimestamp.FromUnixTimestamp(ticket.Timestamp)).TotalMilliseconds)); // This should fix the overflow?
            packet.WriteInteger(ticket.Priority); // Priority
            packet.WriteInteger(ticket.SenderId); // Sender ID
            packet.WriteInteger(1);
            packet.WriteString(ticket.SenderUsername); // Sender Name
            packet.WriteInteger(ticket.ReportedId); // Reported ID
            packet.WriteString(ticket.ReportedUsername); // Reported Name
            packet.WriteInteger(ticket.ModeratorId); // Moderator ID
            packet.WriteString(ticket.ModeratorUsername); // Mod Name
            packet.WriteString(ticket.Issue); // Issue
            packet.WriteUInteger(ticket.RoomId); // Room Id
            packet.WriteInteger(0); //LOOP
        }
        packet.WriteInteger(_userPresets.Count);
        foreach (var pre in _userPresets) packet.WriteString(pre);
        /*base.WriteInteger(UserActionPresets.Count);
        foreach (KeyValuePair<string, List<ModerationPresetActionMessages>> Cat in UserActionPresets.ToList())
        {
            base.WriteString(Cat.Key);
            base.WriteBoolean(true);
            base.WriteInteger(Cat.Value.Count);
            foreach (ModerationPresetActionMessages Preset in Cat.Value.ToList())
            {
                base.WriteString(Preset.Caption);
                base.WriteString(Preset.MessageText);
                base.WriteInteger(Preset.BanTime); // Account Ban Hours
                base.WriteInteger(Preset.IPBanTime); // IP Ban Hours
                base.WriteInteger(Preset.MuteTime); // Mute in Hours
                base.WriteInteger(0);//Trading lock duration
                base.WriteString(Preset.Notice + "\n\nPlease Note: Avatar ban is an IP ban!");
                base.WriteBoolean(false);//Show HabboWay
            }
        }*/

        // TODO: Figure out
        packet.WriteInteger(0);
        {
            //Loop a string.
        }
        // TODO @80O: Implement moderation rights through rank system configurable from database.
        packet.WriteBoolean(true); // Ticket right
        packet.WriteBoolean(true); // Chatlogs
        packet.WriteBoolean(true); // User actions alert etc
        packet.WriteBoolean(true); // Kick users
        packet.WriteBoolean(true); // Ban users
        packet.WriteBoolean(true); // Caution etc
        packet.WriteBoolean(true); // Love you, Tom
        packet.WriteInteger(_roomPresets.Count);
        foreach (var pre in _roomPresets) packet.WriteString(pre);
    }
}