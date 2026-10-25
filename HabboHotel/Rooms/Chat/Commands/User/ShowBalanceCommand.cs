using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :showbal - :bal said out loud, as a blue action bubble the whole
/// room sees (Twist, 2026-10-26). Same bank account (savings), same snapshot
/// (BankUtility.Get, not EnsureLoaded - see BalanceCommand).
///
/// Left off CommandManager's KnockedOutCan and InvisibleCan on purpose: out
/// cold you check nothing, and invisible it would tell the room you are
/// there. Repeats are held back by chat flood control, which counts a
/// command like any other line.
/// </summary>
internal class ShowBalanceCommand : IChatCommand
{
    public string Key => "showbal";
    public string PermissionRequired => "command_showbal";

    public string Parameters => "";

    public string Description => "Show the room how much is in your bank account.";

    /// <summary>The blue action bubble (SuicideCommand.ActionBubble).</summary>
    private const int ActionBubble = 4;

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || room == null)
            return;

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (user == null)
            return;

        var account = BankUtility.Get(habbo.Id);
        if (account == null)
        {
            session.SendWhisper("You do not have a bank account. Head to Mercury Bank to open one.");
            return;
        }

        // Wrapped in "*" so the client narrates it: "*Twist checks their bank
        // account and notices ...*".
        room.SendPacket(new ChatComposer(user.VirtualId,
            $"*checks their bank account and notices they have {TextHandling.GetMoney(account.Savings)} in their account*", 0, ActionBubble));
    }
}
