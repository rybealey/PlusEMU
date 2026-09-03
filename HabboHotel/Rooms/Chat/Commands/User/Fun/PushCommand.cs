using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Fun;

/// <summary>
/// Shove another player one tile directly away from the pusher.
///
/// Reach is the pusher's own tile plus the eight around it (Chebyshev distance
/// &lt;= 1), the same rule :slap uses. From the pusher's own tile there is no
/// "away", so that one case falls back to the direction the pusher faces.
/// </summary>
internal class PushCommand : ITargetChatCommand
{
    public string Key => "push";
    public string PermissionRequired => "command_push";

    public string Parameters => "%target%";

    public string Description => "Push another user.";
    public bool MustBeInSameRoom => true;

    /// <summary>Combat wording: you pick a target, you do not type a username.</summary>
    public string NoTargetMessage => "No target selected.";

    /// <summary>
    /// Blue bubble. Every combat action shares this one style so they read as a
    /// single system at a glance, distinct from ordinary chat and from the white
    /// star bubble the staff RP commands use. Kept in step with SlapCommand's
    /// FightBubble - if a third command needs it, lift it somewhere shared
    /// rather than adding a third copy.
    /// </summary>
    private const int FightBubble = 4;

    /// <summary>Seconds a player must wait between pushes.</summary>
    private const int CooldownSeconds = 5;

    /// <summary>
    /// Last successful push per player id. Commands are DI singletons, so this
    /// instance field is shared hotel-wide; concurrent because rooms tick on
    /// their own threads. Only successful pushes are recorded, so one that
    /// missed for range or had nowhere to go costs nothing.
    /// </summary>
    private readonly ConcurrentDictionary<int, DateTime> _lastPush = new();

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (!room.PushEnabled && !session.GetHabbo().Permissions.HasRight("room_override_custom_config"))
        {
            session.SendWhisper("Oops, it appears that the room owner has disabled the ability to use the push command in here.");
            return Task.CompletedTask;
        }

        if (target == session.GetHabbo())
        {
            session.SendWhisper("Come on, surely you don't want to push yourself.");
            return Task.CompletedTask;
        }

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser == null)
        {
            session.SendWhisper("An error occurred whilst finding that user, maybe they're not online or in this room.");
            return Task.CompletedTask;
        }

        if (targetUser.TeleportEnabled)
        {
            session.SendWhisper("Oops, you cannot push a user whilst they have their teleport mode enabled.");
            return Task.CompletedTask;
        }

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (thisUser == null)
            return Task.CompletedTask;

        if (_lastPush.TryGetValue(session.GetHabbo().Id, out var last))
        {
            var elapsed = (DateTime.UtcNow - last).TotalSeconds;
            if (elapsed < CooldownSeconds)
            {
                // Counts DOWN the seconds still to wait, same as :slap.
                var remaining = (int)Math.Ceiling(CooldownSeconds - elapsed);
                session.SendWhisper($"Cooldown [{remaining}/{CooldownSeconds}]");
                return Task.CompletedTask;
            }
        }

        // Same tile, or one step in any direction including the diagonals.
        if (Math.Abs(targetUser.X - thisUser.X) > 1 || Math.Abs(targetUser.Y - thisUser.Y) > 1)
        {
            session.SendWhisper($"Oops, {target.Username} is not close enough.");
            return Task.CompletedTask;
        }

        // The shove goes directly AWAY from the pusher, not along whichever way
        // the pusher's body happens to point. Nothing makes a player face the
        // target before pushing, and after walking to a tile you face your
        // direction of travel - so a facing-based push sent the target
        // somewhere unrelated, and when the pusher faced away from the target
        // the destination came out as the pusher's OWN tile, which reads as the
        // command doing nothing at all.
        //
        // Reach is Chebyshev <= 1, so each delta is already -1, 0 or 1: its
        // sign IS the tile direction, and no rotation table is needed.
        var offsetX = Math.Sign(targetUser.X - thisUser.X);
        var offsetY = Math.Sign(targetUser.Y - thisUser.Y);

        // Standing on the same tile there is no "away", so fall back to the way
        // the pusher faces. RotBody runs clockwise from 0 = north, and north is
        // -Y on the tile grid, which makes the odd rotations the diagonals -
        // the same mapping Rotation.Calculate produces in reverse.
        if (offsetX == 0 && offsetY == 0)
        {
            (offsetX, offsetY) = thisUser.RotBody switch
            {
                0 => (0, -1),
                1 => (1, -1),
                2 => (1, 0),
                3 => (1, 1),
                4 => (0, 1),
                5 => (-1, 1),
                6 => (-1, 0),
                7 => (-1, -1),
                _ => (0, 0)
            };

            // An out-of-range rotation leaves nowhere to push; say so rather
            // than failing silently.
            if (offsetX == 0 && offsetY == 0)
            {
                session.SendWhisper($"Oops, there is no room to push {target.Username} that way.");
                return Task.CompletedTask;
            }
        }

        var destinationX = (targetUser.X + offsetX);
        var destinationY = (targetUser.Y + offsetY);

        // IsInMap covers all three ways a destination can be unusable in one
        // call: off the edge of the model, not a walkable square, or the door
        // tile. It replaces a check that compared the target's CURRENT x (minus
        // one) against DoorX alone and never looked at DoorY, so it both missed
        // real pushes out of the door and blocked harmless ones anywhere along
        // the door's column.
        if (!room.GetGameMap().IsInMap(destinationX, destinationY))
        {
            session.SendWhisper($"Oops, there is no room to push {target.Username} that way.");
            return Task.CompletedTask;
        }

        _lastPush[session.GetHabbo().Id] = DateTime.UtcNow;
        targetUser.MoveTo(destinationX, destinationY);
        // The wrapping asterisks are what make the client treat a style-4
        // bubble as an action, moving the opening marker ahead of the actor's
        // name to render "*Actor pushes Target*". Sent only once the push has
        // actually been issued, so the room never sees a shove that did not
        // happen.
        room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*pushes {target.Username}*", 0, FightBubble));
        return Task.CompletedTask;
    }
}
