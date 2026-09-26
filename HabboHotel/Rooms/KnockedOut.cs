using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: a knocked-out player (0 health) does nothing. They can still talk -
/// that is how a paramedic gets called - and staff commands still work, since
/// moderation is not roleplay; everything else their avatar would do is
/// refused with the one sentence below, whatever it was.
///
/// Walking needs no check here: CanWalk is already false while out cold. The
/// commands are answered centrally by CommandManager; the packet handlers for
/// the things done by clicking rather than typing - waving, dancing, sitting,
/// using furniture, trading, using an item - each ask Refuse first.
///
/// RpKnockedOut is the room user's own flag, kept from RpHealth by
/// UpdateRpKnockoutState, so asking needs no stats load.
/// </summary>
public static class KnockedOut
{
    public const string Refusal = "You cannot perform this action.";

    /// <summary>Out cold in the room they are in.</summary>
    public static bool Is(GameClient session)
    {
        var habbo = session?.GetHabbo();
        return habbo?.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id)?.RpKnockedOut == true;
    }

    /// <summary>
    /// True, having told them, when the player is out cold and the action must
    /// not happen.
    /// </summary>
    public static bool Refuse(GameClient session)
    {
        if (!Is(session))
            return false;
        session.SendWhisper(Refusal);
        return true;
    }
}
