using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: one player's bars by user id, for a HUD target in another room
/// (RpGetUserStatsEvent) - the same values as RpStatsComposer, which is keyed
/// by a unit in the viewer's own room. Online false and nothing more when the
/// player has logged off.
/// </summary>
public class RpUserStatsComposer : IServerPacket
{
    private readonly int _userId;
    private readonly Habbo? _habbo;

    public uint MessageId => ServerPacketHeader.RpUserStatsComposer;

    public RpUserStatsComposer(Habbo habbo)
    {
        _userId = habbo.Id;
        _habbo = habbo;
    }

    private RpUserStatsComposer(int userId)
    {
        _userId = userId;
    }

    public static RpUserStatsComposer Offline(int userId) => new(userId);

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_userId);
        packet.WriteBoolean(_habbo != null);
        if (_habbo == null)
            return;
        packet.WriteInteger(_habbo.RpHealth);
        packet.WriteInteger(_habbo.RpHealthMax);
        packet.WriteInteger(_habbo.RpEnergy);
        packet.WriteInteger(_habbo.RpEnergyMax);
        packet.WriteInteger((int)Math.Round(_habbo.RpAggression));
        packet.WriteInteger(_habbo.IsRpPassive ? 1 : 0);
        packet.WriteInteger(_habbo.Rank >= 5 ? 1 : 0);
    }
}
