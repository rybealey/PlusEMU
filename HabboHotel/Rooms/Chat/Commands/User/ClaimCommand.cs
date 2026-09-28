using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :claim - start taking this turf for your gang. The same thing the
/// turf panel's Claim button does (RpTurfClaimEvent): both go through
/// TurfManager.TryClaim, which holds every rule about who may start a claim.
/// </summary>
internal class ClaimCommand : IChatCommand
{
    public string Key => "claim";
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Claim this turf for your gang.";

    public void Execute(GameClient session, Room room, string[] parameters) => TurfManager.TryClaim(session, room);
}
