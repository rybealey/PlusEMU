using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :mp - opens the Marketplace, where players sell backpack items to
/// each other (Twist, 2026-10-25). Refused where trading is: knocked out, in
/// cuffs or in jail (the command gates), banned from trading or mid-trade
/// (RpMarket.Open).
/// </summary>
internal class MarketplaceCommand : IChatCommand
{
    public string Key => "mp";
    public string PermissionRequired => "command_mp";

    public string Parameters => "";

    public string Description => "Open the Marketplace to buy and sell backpack items.";

    public void Execute(GameClient session, Room room, string[] parameters) => RpMarket.Open(session);
}
