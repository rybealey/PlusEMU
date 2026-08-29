using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// pixelrp: pushes a room unit's RP stats (health/energy shown in the player
/// HUD) to clients. Keyed by the unit's virtual id (roomIndex client-side).
/// Sent on room entry (both directions) and whenever a stat changes.
/// </summary>
public class RpStatsComposer : IServerPacket
{
    private readonly int _virtualId;
    private readonly int _health;
    private readonly int _healthMax;
    private readonly int _energy;
    private readonly int _energyMax;
    private readonly int _aggression;
    private readonly int _passive;
    private readonly int _staff;

    public uint MessageId => ServerPacketHeader.RpStatsComposer;

    public RpStatsComposer(int virtualId, int health, int healthMax, int energy, int energyMax, int aggression, int passive, int staff)
    {
        _virtualId = virtualId;
        _health = health;
        _healthMax = healthMax;
        _energy = energy;
        _energyMax = energyMax;
        _aggression = aggression;
        _passive = passive;
        _staff = staff;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_virtualId);
        packet.WriteInteger(_health);
        packet.WriteInteger(_healthMax);
        packet.WriteInteger(_energy);
        packet.WriteInteger(_energyMax);
        packet.WriteInteger(_aggression);
        packet.WriteInteger(_passive);
        // staff/verified flag: shows the verified badge in the infostand
        packet.WriteInteger(_staff);
    }
}
