using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: a player walking off of their own accord (Twist, 2026-10-18) -
/// their click to walk (MoveAvatarEvent), or furni they used walking them up
/// to it: a vending machine, the snack machine, an ATM, a bin, dice, a
/// teleporter (UseFurnitureEvent and the other furni-use packets, through
/// <see cref="Use"/>). Being pushed or pulled - moved by somebody else - is
/// not this, and ends none of it.
///
/// What it ends is what walking away ends: an open trade (out loud, RpTrades),
/// a trade request they sent that is still waiting (quietly, OfferState) and a
/// bin search (BinSearch).
/// </summary>
public static class OwnWalk
{
    public static void Started(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        Trading.RpTrades.CancelByWalking(session!);
        Offers.OfferState.WithdrawTradeRequest(habbo.Id);
        BinSearch.Interrupt(habbo.Id);
    }

    /// <summary>
    /// Use a furni for the player, and count any walk the use set off for them
    /// (RoomUser.WalkRequests) as their own walking off.
    /// </summary>
    public static void Use(GameClient session, Room room, Action use)
    {
        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(session?.GetHabbo()?.Id ?? 0);
        var walks = user?.WalkRequests ?? 0;
        use();
        if (user != null && user.WalkRequests != walks)
            Started(session!);
    }
}
