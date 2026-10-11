using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>pixelrp gym: one player's Strength and Stamina levels, for their profile.</summary>
public class RpGymLevelsComposer : IServerPacket
{
    private readonly int _userId;
    private readonly int _strength;
    private readonly int _stamina;

    public uint MessageId => ServerPacketHeader.RpGymLevelsComposer;

    public RpGymLevelsComposer(int userId, int strength, int stamina)
    {
        _userId = userId;
        _strength = strength;
        _stamina = stamina;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_userId);
        packet.WriteInteger(_strength);
        packet.WriteInteger(_stamina);
    }
}
