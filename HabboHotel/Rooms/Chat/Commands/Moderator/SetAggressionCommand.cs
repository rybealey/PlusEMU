using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp RP stats: staff command to set a player's aggression (0-100).
/// Transient — the room tick drains it at 100 points per 45 seconds. Pushes
/// the new value to the room's HUDs and announces via a bubble-23 shout.
/// </summary>
internal class SetAggressionCommand : ITargetChatCommand
{
    public string Key => "setagg";
    public string PermissionRequired => "command_setagg";

    public string Parameters => "%username% %value%";

    public string Description => "Set a player's aggression (0-100, drains over 45s).";

    public bool MustBeInSameRoom => true;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (parameters.Length == 0 || !int.TryParse(parameters[0], out var value))
        {
            session.SendWhisper("Usage: :setagg <username> <value 0-100>");
            return Task.CompletedTask;
        }
        value = Math.Clamp(value, 0, 100);
        target.EnsureRpStatsLoaded();
        target.RpAggression = value;

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser != null)
            room.SendPacket(new RpStatsComposer(targetUser.VirtualId, target.RpHealth, target.RpHealthMax, target.RpEnergy, target.RpEnergyMax, value));

        var adminUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        adminUser?.OnChat(23, $"*{session.GetHabbo().Username} sets {target.Username}'s aggression to {value}*", true);
        return Task.CompletedTask;
    }
}
