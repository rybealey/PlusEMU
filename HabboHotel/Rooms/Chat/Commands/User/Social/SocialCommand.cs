using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Social;

/// <summary>
/// pixelrp social commands: :hug, :kiss and :bite.
///
/// One shape shared by all three - pick someone standing next to you and the
/// room hears the action narrated in the third person. They are relationship
/// actions, not combat: they change nothing, cost nothing and carry their own
/// bubble (16) so they never read as a fight.
///
/// Reach is the actor's own tile plus the eight around it (Chebyshev distance
/// &lt;= 1, diagonals included) - the same adjacency :push uses. The fighting
/// commands are deliberately stricter: :slap and :hit both drop the diagonals,
/// because landing a punch should ask more of you than hugging someone does.
///
/// The text is sent WITHOUT the actor's name and wrapped in asterisks: the
/// client moves the opening marker ahead of the username, rendering
/// "*Yavn wraps their arms around twist, giving them a big hug*".
/// </summary>
internal abstract class SocialCommand : ITargetChatCommand
{
    /// <summary>Bubble 16, the relationship bubble. Shared by all three so
    /// they read as one family, apart from the blue combat bubble.</summary>
    private const int RelationshipBubble = 16;

    /// <summary>Seconds a player must wait between uses, matching :slap.</summary>
    private const int CooldownSeconds = 5;

    /// <summary>
    /// Last successful use per player id. Commands are DI singletons, so this
    /// instance field is shared hotel-wide - and because each concrete command
    /// is its OWN singleton, every one of them keeps its own clock: spamming
    /// :hug is gated, following a hug with a kiss is not. Concurrent because
    /// rooms tick on their own threads. Only a use the room actually heard is
    /// recorded, so one that missed for range costs nothing.
    /// </summary>
    private readonly ConcurrentDictionary<int, DateTime> _lastUse = new();

    public abstract string Key { get; }

    public string PermissionRequired => $"command_{Key}";

    public string Parameters => "%target%";

    public abstract string Description { get; }

    public bool MustBeInSameRoom => true;

    /// <summary>Social wording: you pick a target, you do not type a username.</summary>
    public string NoTargetMessage => "No target selected.";

    /// <summary>The narrated action, from "wraps" onwards - no actor name.</summary>
    protected abstract string Action(string targetName);

    /// <summary>Whispered back when the actor targets themselves.</summary>
    protected abstract string SelfMessage { get; }

    // A missing username, an offline target and a target in another room are
    // all answered by CommandManager before Execute runs.
    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (target == session.GetHabbo())
        {
            session.SendWhisper(SelfMessage);
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

        if (_lastUse.TryGetValue(session.GetHabbo().Id, out var last))
        {
            var elapsed = (DateTime.UtcNow - last).TotalSeconds;
            if (elapsed < CooldownSeconds)
            {
                // Counts DOWN the seconds still to wait, the same way :slap
                // does: [5/5] the instant you retry, [1/5] with under a second
                // to go. Ceiling stops it reading [0/5] while the gate is shut.
                var remaining = (int)Math.Ceiling(CooldownSeconds - elapsed);
                session.SendWhisper($"Cooldown [{remaining}/{CooldownSeconds}]");
                return Task.CompletedTask;
            }
        }

        if (Math.Abs(targetUser.X - thisUser.X) > 1 || Math.Abs(targetUser.Y - thisUser.Y) > 1)
        {
            session.SendWhisper($"You need to be standing next to {target.Username} for that.");
            return Task.CompletedTask;
        }

        _lastUse[session.GetHabbo().Id] = DateTime.UtcNow;
        room.SendPacket(new ShoutComposer(thisUser.VirtualId, $"*{Action(target.Username)}*", 0, RelationshipBubble));
        return Task.CompletedTask;
    }
}
