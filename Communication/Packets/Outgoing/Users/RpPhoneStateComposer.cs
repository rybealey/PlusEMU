using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp phone: pushes the player's saved phone state to their client at
/// login - the preferences document and the notification history, as the two
/// JSON strings described in 91_PhoneState.sql.
///
/// An empty string means nothing has ever been saved for that document. The
/// client reads that as "seed from the copy in this browser's localStorage if
/// there is one, otherwise start from defaults", which is how a player's
/// existing phone migrates onto the server the first time they log in after
/// this ships, rather than coming up factory-reset.
/// </summary>
public class RpPhoneStateComposer : IServerPacket
{
    private readonly string _prefs;
    private readonly string _notifications;

    public uint MessageId => ServerPacketHeader.RpPhoneStateComposer;

    public RpPhoneStateComposer(string prefs, string notifications)
    {
        _prefs = prefs ?? "";
        _notifications = notifications ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_prefs);
        packet.WriteString(_notifications);
    }
}
