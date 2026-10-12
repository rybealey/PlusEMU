using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp: :wander name - forces the target to roam the room in random
/// directions. Re-issuing on someone already wandering turns it off. A player
/// or a bot (not a pet), found as :walk finds them: the name is everything
/// typed, players first.
///
/// The unpredictable counterpart to :walk h/v. That one paces a fixed lane and
/// reverses at the ends, which is readable within a couple of lengths; this
/// picks a fresh direction AND a fresh distance for every leg, and pauses for a
/// random number of cycles between them, so neither the path nor the rhythm
/// settles into something a player can anticipate.
///
/// A player also ends it by walking somewhere themselves, exactly like :walk,
/// and it shares its command_walk permission so no new permission row is
/// needed. A bot's own walking waits while it wanders.
/// </summary>
internal class WanderCommand : IChatCommand
{
    public string Key => "wander";

    public string PermissionRequired => "command_walk";

    public string Parameters => "%name%";

    public string Description => "Force a player or bot to wander the room in random directions.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length < 1)
        {
            session.SendWhisper("Usage: :wander name.");
            return;
        }

        if (room == null)
        {
            session.SendWhisper("You need to be in a room to do that.");
            return;
        }

        var name = string.Join(" ", parameters);
        var manager = room.GetRoomUserManager();
        var target = manager?.GetRoomUserByHabbo(name);
        if (target != null && target.IsBot)
            target = null;
        target ??= manager?.GetBotByName(name);
        if (target == null)
        {
            session.SendWhisper($"{name} is not in this room.");
            return;
        }
        var targetName = target.IsBot ? target.BotData.Name : target.GetUsername();

        if (target.ForcedWalkRandom)
        {
            target.ForcedWalkRandom = false;
            session.SendWhisper($"{targetName} has stopped wandering.");
            return;
        }

        // The two forced modes are mutually exclusive - both act on the same
        // "not currently walking" tick and would otherwise fight for it.
        target.ForcedWalkAxis = null;
        target.ForcedWalkRandom = true;
        session.SendWhisper(target.IsBot
            ? $"{targetName} is now wandering randomly - :wander {targetName} again stops it."
            : $"{targetName} is now wandering randomly - they can break out by walking somewhere themselves.");
    }
}
