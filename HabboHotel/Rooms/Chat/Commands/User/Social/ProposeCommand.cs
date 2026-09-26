using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Offers;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Social;

/// <summary>
/// pixelrp: :propose &lt;player&gt; - ask someone standing next to you to become
/// your partner.
///
/// The answer comes on the offer card above their chat bar, the same card a
/// sale uses (see OfferState.StartProposal): accept and the two of you are
/// partners, each the other's Love row on the profile; decline, ignore it for
/// thirty seconds, or walk away, and nothing happens. While it stands the
/// proposer holds handitem 299.
///
/// One partner at a time, on both sides - somebody already partnered can
/// neither propose nor be proposed to until a :divorce.
///
/// Asked in bubble 16, the relationship bubble :hug and :kiss use, and shouted,
/// because a proposal is a thing the room is meant to see.
/// </summary>
internal class ProposeCommand : ITargetChatCommand
{
    public string Key => "propose";

    public string PermissionRequired => "command_propose";

    public string Parameters => "%target%";

    public string Description => "Propose a partnership to the player standing next to you.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "Propose to who? :propose <player>";

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var proposer = session.GetHabbo();
        if (proposer == null || target == null || room == null)
            return Task.CompletedTask;

        var proposerUser = room.GetRoomUserManager()?.GetRoomUserByHabbo(proposer.Id);
        if (proposerUser == null)
            return Task.CompletedTask;

        var result = OfferState.StartProposal(room, proposerUser, proposer, target, out _);
        switch (result)
        {
            case OfferState.StartResult.Ok:
                break;
            case OfferState.StartResult.Self:
                session.SendWhisper("You cannot propose to yourself.");
                return Task.CompletedTask;
            case OfferState.StartResult.SameAccount:
                session.SendWhisper("That is one of your own characters.");
                return Task.CompletedTask;
            case OfferState.StartResult.TooFar:
                session.SendWhisper($"You need to be standing next to {target.Username} for that.");
                return Task.CompletedTask;
            case OfferState.StartResult.SellerPartnered:
                session.SendWhisper("You are already in a partnership.");
                return Task.CompletedTask;
            case OfferState.StartResult.BuyerPartnered:
                session.SendWhisper($"{target.Username} is already in a partnership.");
                return Task.CompletedTask;
            case OfferState.StartResult.SellerBusy:
                session.SendWhisper("You already have an offer waiting to be answered.");
                return Task.CompletedTask;
            case OfferState.StartResult.BuyerBusy:
                session.SendWhisper($"{target.Username} has too many offers waiting.");
                return Task.CompletedTask;
            default:
                session.SendWhisper("That proposal could not be made.");
                return Task.CompletedTask;
        }

        // The bubble is the proposer's receipt, as it is for :offer - the whole
        // room saw them ask, and the answer is what earns a whisper.
        proposerUser.OnChat(OfferState.RelationshipBubble, $"*gets down on one knee and proposes to {target.Username}*", true);
        return Task.CompletedTask;
    }
}
