using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :wander username - forces the target player to roam the room in
/// random directions. Re-issuing on someone already wandering turns it off.
///
/// The unpredictable counterpart to :walk h/v. That one paces a fixed lane and
/// reverses at the ends, which is readable within a couple of lengths; this
/// picks a fresh direction AND a fresh distance for every leg, and pauses for a
/// random number of cycles between them, so neither the path nor the rhythm
/// settles into something a player can anticipate.
///
/// Ends the moment the player walks somewhere themselves, exactly like :walk,
/// and shares its command_walk permission so no new permission row is needed.
/// </summary>
internal class WanderCommand : IChatCommand
{
    public string Key => "wander";

    public string PermissionRequired => "command_walk";

    public string Parameters => "%username%";

    public string Description => "Force a player to wander the room in random directions.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length < 1)
        {
            session.SendWhisper("Usage: :wander username.");
            return;
        }

        if (room == null)
        {
            session.SendWhisper("You need to be in a room to do that.");
            return;
        }

        var target = room.GetRoomUserManager()?.GetRoomUserByHabbo(parameters[0]);
        if (target == null || target.IsBot)
        {
            session.SendWhisper("That player is not in this room.");
            return;
        }

        if (target.ForcedWalkRandom)
        {
            target.ForcedWalkRandom = false;
            session.SendWhisper($"{target.GetUsername()} has stopped wandering.");
            return;
        }

        // The two forced modes are mutually exclusive - both act on the same
        // "not currently walking" tick and would otherwise fight for it.
        target.ForcedWalkAxis = null;
        target.ForcedWalkRandom = true;
        target.WanderPauseTicks = 0;
        session.SendWhisper(
            $"{target.GetUsername()} is now wandering randomly - they can break out by walking somewhere themselves.");
    }
}
