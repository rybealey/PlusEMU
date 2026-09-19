using Plus.HabboHotel.Corporations;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp: "is this walker clocked in?", for the CorpGate interaction.
///
/// One place, because the answer is needed at four separate context-building
/// sites and getting it wrong at any of them means a gate that leaks. The
/// traversal predicate itself stays pure - it only ever reads the bool that was
/// resolved here.
///
/// FAILS CLOSED for a walker but OPEN for a non-walker. A RoomUser with no
/// client is a bot or a pet, which is not somebody a duty rota applies to and
/// which must keep being able to cross; a real player whose duty cannot be
/// established is treated as off duty, because a gate that guesses "yes" is
/// not a gate.
/// </summary>
internal static class MovementDuty
{
    public static bool IsOnDuty(RoomUser? user)
    {
        if (user == null)
            return true;
        // IsBot covers pets too - RoomUser.IsPet is IsBot plus a flag.
        if (user.IsBot)
            return true;

        var habbo = user.GetClient()?.GetHabbo();
        if (habbo == null)
            return false;

        return ShiftManager.IsOnDuty(habbo.Id);
    }

    /// <summary>
    /// The same question from inside the movement engine, which knows a walker
    /// by its virtual id rather than by its RoomUser.
    /// </summary>
    public static bool IsOnDuty(Room? room, int virtualId)
    {
        if (room == null)
            return true;
        return IsOnDuty(room.GetRoomUserManager()?.GetRoomUserByVirtualId(virtualId));
    }
}
