using System.Collections.Concurrent;
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
/// Reach is the slapper's own tile plus the eight surrounding it (Chebyshev
/// distance &lt;= 1 - the full 3x3 block, diagonals included). That is the same
/// adjacency rule :push applies, so there is only one reach rule to learn.
/// </summary>
internal class SlapCommand : ITargetChatCommand
{
    public string Key => "slap";
    public string PermissionRequired => "command_slap";

    public string Parameters => "%target%";

    public string Description => "Slap another user across the face.";

    public bool MustBeInSameRoom => true;

    /// <summary>Combat wording: you pick a target, you do not type a username.</summary>
    public string NoTargetMessage => "No target selected.";

    /// <summary>
    /// Blue bubble. Combat actions share one style so they read as a single
    /// system at a glance, distinct from ordinary chat and from the white
    /// star bubble the staff RP commands use.
    /// </summary>
    private const int FightBubble = 4;

    /// <summary>Seconds a player must wait between slaps.</summary>
    private const int CooldownSeconds = 5;

    /// <summary>
    /// Last successful slap per player id. Commands are DI singletons, so this
    /// instance field is shared hotel-wide; concurrent because rooms tick on
    /// their own threads. Only successful slaps are recorded, so one that missed
    /// for range costs nothing.
    /// </summary>
    private readonly ConcurrentDictionary<int, DateTime> _lastSlap = new();

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

        if (_lastSlap.TryGetValue(session.GetHabbo().Id, out var last))
        {
            var elapsed = (DateTime.UtcNow - last).TotalSeconds;
            if (elapsed < CooldownSeconds)
            {
                // Counts DOWN the seconds still to wait: [5/5] the instant you
                // retry, [1/5] with under a second to go. Ceiling stops it ever
                // reading [0/5] while the gate is still shut.
                var remaining = (int)Math.Ceiling(CooldownSeconds - elapsed);
                session.SendWhisper($"Cooldown [{remaining}/{CooldownSeconds}]");
                return Task.CompletedTask;
            }
        }

        // Same tile, or one step in any direction including the diagonals.
        // :push spells the same test inside-out (|dx| >= 2 || |dy| >= 2).
        if (Math.Abs(targetUser.X - thisUser.X) > 1 || Math.Abs(targetUser.Y - thisUser.Y) > 1)
        {
            session.SendWhisper($"Oops, {target.Username} is not close enough.");
            return Task.CompletedTask;
        }

        // Leading AND trailing "*" matter: the client only treats a style-4
        // bubble as an action when the text is wrapped in them, and it then
        // moves the opening marker ahead of the actor's name, rendering
        // "*Actor slaps Target across the face*".
        _lastSlap[session.GetHabbo().Id] = DateTime.UtcNow;
        room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*slaps {target.Username} across the face*", 0, FightBubble));
        return Task.CompletedTask;
    }
}
