using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Offers;
using Plus.HabboHotel.Users.Relationships;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Social;

/// <summary>
/// pixelrp: :divorce - end your partnership.
///
/// Unilateral and instant: it takes one of the two to end it, anywhere, whether
/// or not the other is online. Their Love rows go from both profiles at once,
/// and both are free to :propose again.
///
/// Said in the offer bubble (5) - a proposal and a yes to it are the
/// relationship bubble's; the end of one is not.
/// </summary>
internal class DivorceCommand : IChatCommand
{
    public string Key => "divorce";

    public string PermissionRequired => "command_divorce";

    public string Parameters => string.Empty;

    public string Description => "End your partnership.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;

        var exId = PartnershipUtility.Divorce(habbo.Id);
        if (exId == 0)
        {
            session.SendWhisper("You are not in a partnership.");
            return;
        }

        var exSession = PlusEnvironment.Game.ClientManager.GetClientByUserId(exId);
        var ex = exSession?.GetHabbo();
        var exName = ex?.Username ?? PartnershipUtility.NameOf(exId);

        var user = room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        user?.OnChat(OfferState.OfferBubble, $"*divorces {exName}*", true);
        exSession?.SendWhisper($"{habbo.Username} has divorced you.");

        PartnershipUtility.PushRelationships(room, habbo.Id);
        PartnershipUtility.PushRelationships(room, exId);
        // The ex may be somewhere else entirely; their own room hears it too,
        // so a card open on them over there loses its heart as well.
        if (ex?.CurrentRoom != null && ex.CurrentRoom != room)
        {
            PartnershipUtility.PushRelationships(ex.CurrentRoom, habbo.Id);
            PartnershipUtility.PushRelationships(ex.CurrentRoom, exId);
        }
    }
}
