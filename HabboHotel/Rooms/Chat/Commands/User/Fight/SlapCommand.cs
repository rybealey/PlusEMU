using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Fight;

/// <summary>
/// pixelrp fighting system: slap another player.
///
/// First of the combat actions and deliberately inert - it deals NO damage
/// yet, it only emits the action bubble. Health lives on Habbo.RpHealth and is
/// pushed to HUDs by RpStatsComposer, so wiring damage in later is a matter of
/// adjusting that and re-broadcasting.
///
/// Reach is the slapper's own tile plus the four orthogonal neighbours
/// (Manhattan distance &lt;= 1). Diagonals are out of reach on purpose, which
/// makes this stricter than :push (that one uses a Chebyshev check).
/// </summary>
internal class SlapCommand : ITargetChatCommand
{
    public string Key => "slap";
    public string PermissionRequired => "command_slap";

    public string Parameters => "%target%";

    public string Description => "Slap another user across the face.";

    public bool MustBeInSameRoom => true;

    /// <summary>
    /// Blue bubble. Combat actions share one style so they read as a single
    /// system at a glance, distinct from ordinary chat and from the white
    /// star bubble the staff RP commands use.
    /// </summary>
    private const int FightBubble = 4;

    // Missing username, an offline target and a target in another room are all
    // answered by CommandManager before Execute runs, so they are not repeated
    // here.
    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (target == session.GetHabbo())
        {
            session.SendWhisper("You cannot slap yourself.");
            return Task.CompletedTask;
        }

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser == null)
        {
            session.SendWhisper($"{target.Username} is not in this room.");
            return Task.CompletedTask;
        }

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (thisUser == null)
            return Task.CompletedTask;

        // Same tile, or one step N/E/S/W - never diagonal.
        if (Math.Abs(targetUser.X - thisUser.X) + Math.Abs(targetUser.Y - thisUser.Y) > 1)
        {
            session.SendWhisper($"{target.Username} is not close enough to slap.");
            return Task.CompletedTask;
        }

        room.SendPacket(new ChatComposer(thisUser.VirtualId, $"slaps *{target.Username} across the face*", 0, FightBubble));
        return Task.CompletedTask;
    }
}
