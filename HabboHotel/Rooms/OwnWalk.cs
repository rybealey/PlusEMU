using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Banking;

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
/// a trade request, :offer or :sell they sent that is still waiting (quietly,
/// OfferState), a bin search (BinSearch), an officer's 911 trip still
/// counting down (out loud, EmergencyCalls), and the windows that belong to
/// where the player stands (<see cref="CloseStandingWindows"/>).
/// </summary>
public static class OwnWalk
{
    public static void Started(GameClient session)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        Trading.RpTrades.CancelByWalking(session!);
        Offers.OfferState.WithdrawOnWalkingOff(habbo.Id);
        BinSearch.Interrupt(habbo.Id);
        Plus.HabboHotel.Corporations.EmergencyCalls.CancelTripByWalking(session!);
        CloseStandingWindows(session!, habbo);
    }

    /// <summary>
    /// The ATM, the deposit box and the dressing booth's clothes editor close
    /// at the click to walk (Twist, 2026-10-25). Their own closes fire when the
    /// step off the tile LANDS - RoomUser.X/Y stays the tile being left until
    /// then - so they trailed the avatar by half a second or more. Those stay,
    /// for being moved by somebody else; a second close is harmless.
    /// </summary>
    private static void CloseStandingWindows(GameClient session, Habbo habbo)
    {
        AtmSessions.WalkedAway(session);
        var room = habbo.CurrentRoom;
        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        var items = (user == null) ? null : room!.GetGameMap()?.GetAllRoomItemForSquare(user.X, user.Y);
        if (items == null)
            return;
        // Sent at once rather than queued like DepositBox.CloseSoon: a close
        // needs no database. Should an open still be queued from a step on a
        // moment ago, the step-off close queued behind it closes it again.
        if (items.Any(item => item?.Definition?.InteractionType == InteractionType.DepositBox))
            DepositBox.Close(session);
        if (items.Any(item => item?.Definition?.InteractionType == InteractionType.DressingBooth))
            session.Send(new InClientLinkComposer("avatar-editor/hide"));
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
