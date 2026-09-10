using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the wanted list - every player with an open charge, with the
/// wanted level their worst charge earns them.
///
/// One packet serves two surfaces. The Wanted window renders it as a list, and
/// the player HUD looks each id up to draw stars over whoever you are looking
/// at, which is why it carries the whole list rather than just the recipient's
/// own level. Before this the HUD's stars came from a hash of the username -
/// stable and meaningless.
///
/// A count of zero is a legitimate and common payload: nobody is wanted.
/// </summary>
public class RpWantedComposer : IServerPacket
{
    private readonly List<WantedUtility.WantedPlayer> _wanted;

    public uint MessageId => ServerPacketHeader.RpWantedComposer;

    public RpWantedComposer(List<WantedUtility.WantedPlayer> wanted)
    {
        _wanted = wanted ?? new List<WantedUtility.WantedPlayer>();
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_wanted.Count);
        foreach (var player in _wanted)
        {
            packet.WriteInteger(player.UserId);
            packet.WriteString(player.Username);
            packet.WriteString(player.Figure);
            packet.WriteInteger(player.Level);
            packet.WriteInteger(player.Since);
        }
    }
}
