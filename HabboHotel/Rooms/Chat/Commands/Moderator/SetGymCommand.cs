using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Gym;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp gym: staff command to set a player's Strength or Stamina level
/// (0-10). The level's progress is cleared, and a Stamina level sets max
/// health and restarts the drop's clock (GymState.SetLevelForStaff). It sets
/// levels only - there is no skipping the membership or the 30-hour clock.
/// Announced like :sethp.
/// </summary>
internal class SetGymCommand : ITargetChatCommand
{
    public string Key => "setgym";
    public string PermissionRequired => "command_setgym";

    public string Parameters => "%username% %stat% %level%";

    public string Description => "Set a player's Strength or Stamina level.";

    public bool MustBeInSameRoom => true;

    private const string Usage = "Usage: :setgym <username> <strength|stamina> <0-10>";

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (parameters.Length < 2 || !int.TryParse(parameters[1], out var level))
        {
            session.SendWhisper(Usage);
            return Task.CompletedTask;
        }

        GymState.Stat stat;
        switch (parameters[0].ToLowerInvariant())
        {
            case "strength":
                stat = GymState.Stat.Strength;
                break;
            case "stamina":
                stat = GymState.Stat.Stamina;
                break;
            default:
                session.SendWhisper(Usage);
                return Task.CompletedTask;
        }

        level = Math.Clamp(level, 0, GymState.MaxLevel);
        GymState.SetLevelForStaff(target, stat, level);

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser != null && stat == GymState.Stat.Stamina)
            room.SendPacket(new RpStatsComposer(targetUser.VirtualId, target.RpHealth, target.RpHealthMax, target.RpEnergy, target.RpEnergyMax, (int)Math.Round(target.RpAggression), target.IsRpPassive ? 1 : 0, target.Rank >= 5 ? 1 : 0));

        var adminUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        adminUser?.OnChat(23, $"*sets {target.Username}'s {(stat == GymState.Stat.Stamina ? "Stamina" : "Strength")} to level {level}*", true);
        return Task.CompletedTask;
    }
}
