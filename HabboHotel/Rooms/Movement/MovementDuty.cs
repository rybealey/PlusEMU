using Plus.HabboHotel.Corporations;

namespace Plus.HabboHotel.Rooms.Movement;

/// <summary>
/// pixelrp: "is this walker clocked in?", for the CorpGate interaction.
///
/// One place, because getting it wrong anywhere means a gate that leaks. It
/// used to be asked at four separate context-building sites; it is now asked
/// once, by MovementWalkerContext. The traversal predicate itself stays pure -
/// it only ever reads the bool that was resolved here.
///
/// Asked about a RoomUser, never about a virtual id: resolving an id to a
/// walker is <see cref="MovementWalkerContext"/>'s job, and it asks this while
/// it has the user in hand. An id-taking overload used to live here, and
/// having it invited exactly the bug that helper exists to prevent - a context
/// built with duty resolved and the walker's other permissions left false.
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
}
