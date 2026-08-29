using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp RP stats: staff command to fully restore a player's health and
/// energy. Persists, pushes the new values to the room's HUDs and announces
/// via a bubble-23 shout.
/// </summary>
internal class RestoreCommand : ITargetChatCommand
{
    public string Key => "restore";
    public string PermissionRequired => "command_restore";

    public string Parameters => "%username%";

    public string Description => "Fully restore a player's health and energy.";

    public bool MustBeInSameRoom => true;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        target.EnsureRpStatsLoaded();
        target.RpHealth = target.RpHealthMax;
        target.RpEnergy = target.RpEnergyMax;
        target.SaveRpStats();

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser != null)
        {
            room.SendPacket(new RpStatsComposer(targetUser.VirtualId, target.RpHealth, target.RpHealthMax, target.RpEnergy, target.RpEnergyMax, (int)Math.Round(target.RpAggression), target.RpPassiveSeconds > 0 ? 1 : 0, target.Rank >= 5 ? 1 : 0));
            room.GetRoomUserManager().ApplyRpKnockout(targetUser);
        }

        var adminUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        adminUser?.OnChat(23, $"*restores {target.Username}'s health and energy levels*", true);
        return Task.CompletedTask;
    }
}
