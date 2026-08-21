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

    public uint MessageId => ServerPacketHeader.RpStatsComposer;

    public RpStatsComposer(int virtualId, int health, int healthMax, int energy, int energyMax)
    {
        _virtualId = virtualId;
        _health = health;
        _healthMax = healthMax;
        _energy = energy;
        _energyMax = energyMax;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_virtualId);
        packet.WriteInteger(_health);
        packet.WriteInteger(_healthMax);
        packet.WriteInteger(_energy);
        packet.WriteInteger(_energyMax);
    }
}
