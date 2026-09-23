using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Offers;
using Plus.HabboHotel.Users;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :offer &lt;player&gt; &lt;item&gt; [quantity] - sell somebody something.
///
/// The hospital first: a medkit out of a packet of painkillers, or a shot from
/// a syringe. Both are gated on being clocked in AND holding the tool, because
/// the goods are MINTED rather than handed over (see OfferState) - a medic with
/// nothing in their hands would otherwise be an infinite supply.
///
/// Quantity is optional and means one. A heal ignores it entirely: half a shot
/// is not a thing, and two at once is just the second one wasted.
///
/// Nothing is escrowed. This only puts a card on the other player's screen;
/// every rule is asked again when they answer it.
/// </summary>
internal class OfferCommand : ITargetChatCommand
{
    public virtual string Key => "offer";

    public string PermissionRequired => "command_offer";

    public string Parameters => "%target% %item% %quantity%";

    public string Description => "Sell an item or a service to another player.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "Offer it to who? :offer <player> <item> [quantity]";

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var seller = session.GetHabbo();
        if (seller == null || target == null || room == null)
            return Task.CompletedTask;

        if (parameters.Length < 1)
        {
            session.SendWhisper($"Offer them what? {Sellables()}");
            return Task.CompletedTask;
        }

        var itemKey = parameters[0];
        if (!OfferState.Catalogue.TryGetValue(itemKey, out var ware))
        {
            session.SendWhisper($"'{parameters[0]}' is not something you can sell. {Sellables()}");
            return Task.CompletedTask;
        }

        // Missing means one. A number that is not a number is worth saying out
        // loud rather than quietly treating as one - "2x" was a typo for
        // something, and silently selling one of it is the wrong repair.
        var quantity = 1;
        if (ware.TakesQuantity && parameters.Length >= 2)
        {
            if (!int.TryParse(parameters[1], out quantity))
            {
                session.SendWhisper("That is not a quantity. Whole numbers only.");
                return Task.CompletedTask;
            }
        }

        var sellerUser = room.GetRoomUserManager()?.GetRoomUserByHabbo(seller.Id);
        var result = OfferState.Start(room, sellerUser, seller, target, ware.Key, quantity, out var offer);

        switch (result)
        {
            case OfferState.StartResult.Ok:
                break;
            case OfferState.StartResult.Self:
                session.SendWhisper("Sell it to somebody else.");
                return Task.CompletedTask;
            case OfferState.StartResult.SameAccount:
                session.SendWhisper("That is one of your own characters.");
                return Task.CompletedTask;
            case OfferState.StartResult.NotStaff:
                // The same three-way split MedicalUtility already words for the
                // ambulance: a civilian, an employee off the clock, and the rest.
                session.SendWhisper(MedicalUtility.IsHospitalStaff(seller.Id)
                    ? "You have to be on duty to sell that. Clock in from the Corporations drawer."
                    : "Only hospital staff can sell that.");
                return Task.CompletedTask;
            case OfferState.StartResult.NoTool:
                session.SendWhisper($"You need to be holding {ware.RequiredHandItemName} to offer that.");
                return Task.CompletedTask;
            case OfferState.StartResult.BadQuantity:
                session.SendWhisper($"Between 1 and {OfferState.MaxQuantity} at a time.");
                return Task.CompletedTask;
            case OfferState.StartResult.SellerBusy:
                session.SendWhisper("You already have an offer waiting to be answered.");
                return Task.CompletedTask;
            case OfferState.StartResult.BuyerBusy:
                session.SendWhisper($"{target.Username} has too many offers waiting.");
                return Task.CompletedTask;
            default:
                session.SendWhisper("That offer could not be made.");
                return Task.CompletedTask;
        }

        // Bubble 5 wrapped in asterisks, the shape :give already uses - the
        // client reads it as an action and moves the opening marker ahead of
        // the speaker's name, so this renders as "*Ryan offers Twist ...*".
        var sum = (offer!.Total > 0) ? $" for {TextHandling.GetMoney(offer.Total)}" : string.Empty;
        var goods = ware.TakesQuantity
            ? $"{TextHandling.GetNumber(offer.Quantity)} {offer.Label}"
            : $"a {ware.One.ToLowerInvariant()}";
        sellerUser?.OnChat(5, $"*offers {target.Username} {goods}{sum}*", true);
        session.SendWhisper($"Offered to {target.Username}. They have {OfferState.LifetimeSeconds} seconds to answer.");
        return Task.CompletedTask;
    }

    private static string Sellables() =>
        "Try: " + string.Join(", ", OfferState.Catalogue.Keys);
}

/// <summary>
/// pixelrp: :sell, the same command under the word most people reach for.
///
/// A subclass rather than a registration, because the command manager keys
/// everything it finds by its own Key and finds both of these by the DI scan -
/// so an alias IS another command object, and this is the whole of one.
/// </summary>
internal class SellCommand : OfferCommand
{
    public override string Key => "sell";
}
