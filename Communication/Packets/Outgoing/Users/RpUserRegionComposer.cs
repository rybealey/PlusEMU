using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: one player's region, as the code stored on `users.rp_region`
/// ('na', 'eu', 'oc', or '' for not set - see 96_UserRegion.sql).
///
/// Carries the user id because the client caches these by id the way it caches
/// employment: the profile window asks for whoever it is showing, and a player
/// changing their own region broadcasts to the room so open profiles and
/// anything else keyed on it update without asking again.
/// </summary>
public class RpUserRegionComposer : IServerPacket
{
    private readonly int _userId;
    private readonly string _region;

    public uint MessageId => ServerPacketHeader.RpUserRegionComposer;

    public RpUserRegionComposer(int userId, string region)
    {
        _userId = userId;
        _region = region ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_userId);
        packet.WriteString(_region);
    }
}
