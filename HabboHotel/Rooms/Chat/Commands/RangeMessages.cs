namespace Plus.HabboHotel.Rooms.Chat.Commands;

/// <summary>
/// pixelrp: the two whispers every range-based action answers with - a
/// command that must be done standing near someone (ITargetChatCommand.IsRanged),
/// or handing them something. One wording everywhere, and the default for any
/// range-based action added later. An attack that swings anyway (:hit, :stun,
/// :spit, pepper spray) misses in public instead of saying it is too far.
/// </summary>
public static class RangeMessages
{
    public const string NotInRoom = "That user is not in this room.";

    public static string TooFar(string username) => $"You are too far away from {username}.";
}
