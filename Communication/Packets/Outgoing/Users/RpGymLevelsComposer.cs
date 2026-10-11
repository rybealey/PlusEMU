using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Gym;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp gym: one player's Strength and Stamina levels, for their profile -
/// the user id and the two levels, then the XP toward each next level and
/// what it takes (0 / 0 at level 10). XP is last, so a client from before it
/// still reads the levels.
/// </summary>
public class RpGymLevelsComposer : IServerPacket
{
    private readonly int _userId;
    private readonly GymState.Levels _levels;

    public uint MessageId => ServerPacketHeader.RpGymLevelsComposer;

    public RpGymLevelsComposer(int userId, GymState.Levels levels)
    {
        _userId = userId;
        _levels = levels;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_userId);
        packet.WriteInteger(_levels.Strength);
        packet.WriteInteger(_levels.Stamina);
        packet.WriteInteger(_levels.StrengthXp);
        packet.WriteInteger(_levels.StrengthXpNeeded);
        packet.WriteInteger(_levels.StaminaXp);
        packet.WriteInteger(_levels.StaminaXpNeeded);
    }
}
