using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Fun;

/// <summary>
/// Shove another player up to three tiles directly away from the pusher.
///
/// Reach is the pusher's own tile plus the eight around it (Chebyshev distance
/// &lt;= 1), the same rule :push and :slap use. From the pusher's own tile there
/// is no "away", so that one case falls back to the direction the pusher faces.
///
/// The shove travels outward one tile at a time and stops at the last tile that
/// can be stood on, so a target with a wall two tiles behind them is pushed the
/// one tile that IS free rather than the whole command failing.
/// </summary>
internal class SuperPushCommand : ITargetChatCommand
{
    public string Key => "spush";
    public string PermissionRequired => "command_super_push";

    public string Parameters => "%target%";

    public string Description => "Superpush another user. (Pushes them 3 squares away)";
    public bool MustBeInSameRoom => true;

    /// <summary>Combat wording: you pick a target, you do not type a username.</summary>
    public string NoTargetMessage => "No target selected.";

    /// <summary>
    /// Blue bubble. Every combat action shares this one style so they read as a
    /// single system at a glance, distinct from ordinary chat and from the white
    /// star bubble the staff RP commands use. Kept in step with SlapCommand and
    /// PushCommand - three copies now, so the next one to need it should lift it
    /// somewhere shared.
    /// </summary>
    private const int FightBubble = 4;

    /// <summary>Seconds a player must wait between super pushes.</summary>
    private const int CooldownSeconds = 5;

    /// <summary>How far a super push travels when nothing is in the way.</summary>
    private const int PushDistance = 3;

    /// <summary>
    /// Last successful super push per player id. Commands are DI singletons, so
    /// this instance field is shared hotel-wide; concurrent because rooms tick
    /// on their own threads. Only successful pushes are recorded, so one that
    /// missed for range or had nowhere to go costs nothing.
    /// </summary>
    private readonly ConcurrentDictionary<int, DateTime> _lastSuperPush = new();

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (!room.SuperPushEnabled && !room.CheckRights(session, true) && !session.GetHabbo().Permissions.HasRight("room_override_custom_config"))
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

        if (_lastSuperPush.TryGetValue(session.GetHabbo().Id, out var last))
        {
            var elapsed = (DateTime.UtcNow - last).TotalSeconds;
            if (elapsed < CooldownSeconds)
            {
                // Counts DOWN the seconds still to wait, same as :push and :slap.
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

        // Direction is AWAY from the pusher, not along whichever way the
        // pusher's body happens to point. Nothing makes a player face their
        // target first, so a facing-based push sent the target somewhere
        // unrelated, and when the pusher faced away from them it resolved to
        // the pusher's own tile - which read as the command doing nothing.
        //
        // Reach is Chebyshev <= 1, so each delta is already -1, 0 or 1 and its
        // sign IS the tile direction.
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

        // Walk the shove outward tile by tile and keep the furthest one that can
        // actually be stood on, stopping at the first that cannot. Checking each
        // step rather than only the final tile means a super push cannot jump
        // someone over a wall, a hole or the door, and a partially blocked shove
        // still moves them as far as it can.
        //
        // IsInMap covers every way a tile can be unusable in one call: off the
        // edge of the model, not a walkable square, and the door - the dynamic
        // door because its map byte is 3 rather than 1, and a relocated static
        // door because IsInMap tests those coordinates outright. That replaces
        // three checks which compared the target's CURRENT x or y (minus one,
        // two and three) against DoorX or DoorY independently, so they refused
        // any push while the target stood within three tiles of the door's row
        // OR column, and never looked at where the target was going.
        var map = room.GetGameMap();
        var destinationX = targetUser.X;
        var destinationY = targetUser.Y;

        for (var step = 1; step <= PushDistance; step++)
        {
            var candidateX = (targetUser.X + (offsetX * step));
            var candidateY = (targetUser.Y + (offsetY * step));
            if (!map.IsInMap(candidateX, candidateY))
                break;
            destinationX = candidateX;
            destinationY = candidateY;
        }

        // Not even the first tile was free.
        if (destinationX == targetUser.X && destinationY == targetUser.Y)
        {
            session.SendWhisper($"Oops, there is no room to push {target.Username} that way.");
            return Task.CompletedTask;
        }

        _lastSuperPush[session.GetHabbo().Id] = DateTime.UtcNow;
        targetUser.MoveTo(destinationX, destinationY);
        // The wrapping asterisks are what make the client treat a style-4
        // bubble as an action, moving the opening marker ahead of the actor's
        // name to render "*Actor super pushes Target*". Sent only once the push
        // has actually been issued, so the room never sees a shove that did not
        // happen.
        room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*super pushes {target.Username}*", 0, FightBubble));
        return Task.CompletedTask;
    }
}
