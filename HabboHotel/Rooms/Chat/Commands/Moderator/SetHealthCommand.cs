using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp RP stats: staff command to set a player's health. Persists, pushes
/// the new value to the room's HUDs and announces via a bubble-23 shout.
/// </summary>
internal class SetHealthCommand : ITargetChatCommand
{
    public string Key => "sethp";
    public string PermissionRequired => "command_sethp";

    public string Parameters => "%username% %value%";

    public string Description => "Set a player's health.";

    public bool MustBeInSameRoom => true;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (parameters.Length == 0 || !int.TryParse(parameters[0], out var value))
        {
            session.SendWhisper("Usage: :sethp <username> <value>");
            return Task.CompletedTask;
        }
        target.EnsureRpStatsLoaded();
        value = Math.Clamp(value, 0, target.RpHealthMax);
        target.RpHealth = value;
        target.SaveRpStats();

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser != null)
        {
            room.SendPacket(new RpStatsComposer(targetUser.VirtualId, target.RpHealth, target.RpHealthMax, target.RpEnergy, target.RpEnergyMax, (int)Math.Round(target.RpAggression), target.RpPassiveSeconds > 0 ? 1 : 0, target.Rank >= 5 ? 1 : 0));
            room.GetRoomUserManager().ApplyRpKnockout(targetUser);
        }

        var adminUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        adminUser?.OnChat(23, $"*{session.GetHabbo().Username} sets {target.Username}'s health to {value}*", true);
        return Task.CompletedTask;
    }
}
