using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Privacy;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: a player's OWN profile privacy settings, for the phone's Privacy
/// screen. Sent at login and echoed after every save.
///
/// Only ever the recipient's own: nobody is told what anybody else has chosen,
/// which is the point. What another player may see is answered by giving or
/// withholding the data itself, never by telling the asker a rule.
/// </summary>
public class RpPrivacyComposer : IServerPacket
{
    private readonly PrivacyUtility.ProfilePrivacy _privacy;

    public uint MessageId => ServerPacketHeader.RpPrivacyComposer;

    public RpPrivacyComposer(PrivacyUtility.ProfilePrivacy privacy)
    {
        _privacy = privacy ?? PrivacyUtility.Default;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_privacy.Birthday);
        packet.WriteInteger(_privacy.Region);
        packet.WriteInteger(_privacy.RegionColleagues ? 1 : 0);
        packet.WriteInteger(_privacy.RegionFriends ? 1 : 0);
    }
}
