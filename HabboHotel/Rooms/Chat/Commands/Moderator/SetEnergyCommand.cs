using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp RP stats: staff command to set a player's energy. Persists, pushes
/// the new value to the room's HUDs and announces via a bubble-23 shout.
/// </summary>
internal class SetEnergyCommand : ITargetChatCommand
{
    public string Key => "seten";
    public string PermissionRequired => "command_seten";

    public string Parameters => "%username% %value%";

    public string Description => "Set a player's energy.";

    public bool MustBeInSameRoom => true;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (parameters.Length == 0 || !int.TryParse(parameters[0], out var value))
        {
            session.SendWhisper("Usage: :seten <username> <value>");
            return Task.CompletedTask;
        }
        target.EnsureRpStatsLoaded();
        value = Math.Clamp(value, 0, target.RpEnergyMax);
        target.RpEnergy = value;
        target.SaveRpStats();

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser != null)
            room.SendPacket(new RpStatsComposer(targetUser.VirtualId, target.RpHealth, target.RpHealthMax, target.RpEnergy, target.RpEnergyMax, (int)Math.Round(target.RpAggression), target.RpPassiveSeconds > 0 ? 1 : 0));

        var adminUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        adminUser?.OnChat(23, $"*{session.GetHabbo().Username} sets {target.Username}'s energy to {value}*", true);
        return Task.CompletedTask;
    }
}
