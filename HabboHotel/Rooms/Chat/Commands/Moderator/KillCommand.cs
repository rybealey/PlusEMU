using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp RP stats: staff command to instantly knock a player out — health
/// drops to 0, which forces the frozen lay (see RoomUser knockout state).
/// Persists, pushes the new value to the room's HUDs and announces via a
/// bubble-23 shout.
/// </summary>
internal class KillCommand : ITargetChatCommand
{
    public string Key => "kill";
    public string PermissionRequired => "command_kill";

    public string Parameters => "%username%";

    public string Description => "Instantly knock a player out.";

    public bool MustBeInSameRoom => true;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        target.EnsureRpStatsLoaded();
        target.RpHealth = 0;
        target.SaveRpStats();

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser != null)
        {
            room.SendPacket(new RpStatsComposer(targetUser.VirtualId, target.RpHealth, target.RpHealthMax, target.RpEnergy, target.RpEnergyMax, (int)Math.Round(target.RpAggression), target.RpPassiveSeconds > 0 ? 1 : 0));
            room.GetRoomUserManager().ApplyRpKnockout(targetUser);
        }

        var adminUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        adminUser?.OnChat(23, $"*summons a bolt of lightning, knocking {target.Username} out*", true);
        return Task.CompletedTask;
    }
}
