using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

namespace Plus.HabboHotel.Users;

/// <summary>
/// pixelrp: the states that must pin a player in place. Walking is blocked by
/// the client while cuffed, escorted, knocked out or jailed, but a player can
/// still be MOVED by a server-initiated travel - follow a friend, find new
/// friends, answer a call - and those have to ask too, or following a friend
/// is a way out of handcuffs. (A single home for this; the police and trade
/// code grew their own copies, which a later pass can fold into this one.)
/// </summary>
public static class TravelGuard
{
    /// <summary>Why this player may not travel right now, or null if they may.</summary>
    public static string? Refusal(GameClient? session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return "You are not here.";
        var id = habbo.Id;
        if (KnockedOut.Is(session!))
            return "You are knocked out.";
        if (PoliceState.IsCuffed(id))
            return "Your hands are cuffed.";
        if (PoliceState.IsBeingEscorted(id))
            return "You are being escorted.";
        if (PoliceState.IsEscorting(id))
            return "You cannot leave while escorting someone.";
        if (JailState.IsJailed(id))
            return "You cannot leave jail.";
        return null;
    }
}
