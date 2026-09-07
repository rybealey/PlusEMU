using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.AvatarEffects;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.Database;
using Plus.HabboHotel.Catalog.Clothing;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: buy what is on the Clothing Store's mannequin - a list of
/// catalog_clothing ids. Every check runs before a credit moves (the
/// RpBuyGangEvent rule): unknown or already-owned pieces drop out, sold-out
/// editions and an overdrawn balance fail the whole basket, and a limited
/// edition needs a free backpack slot for its token. Regular pieces become
/// user_clothing rows and the FigureSetIds list is re-sent so Choose Your
/// Looks unlocks them at once; limited editions land as
/// "clothing:&lt;id&gt;:&lt;edition&gt;" backpack tokens, redeemed in RpUseItemEvent.
/// </summary>
internal class RpBuyClothingEvent : IPacketEvent
{
    private const int MaxBasket = 20;
    /// <summary>Backpack item key prefix for a clothing token.</summary>
    public const string TokenPrefix = "clothing:";

    private readonly IClothingManager _clothingManager;
    private readonly IDatabase _database;

    public RpBuyClothingEvent(IClothingManager clothingManager, IDatabase database)
    {
        _clothingManager = clothingManager;
        _database = database;
    }

    public static bool OwnsClothing(HabboHotel.Users.Habbo habbo, ClothingItem clothing)
        => clothing.PartIds.All(partId => habbo.Clothing.TryGet(partId, out _));

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var count = Math.Clamp(packet.ReadInt(), 0, MaxBasket);
        var basket = new List<ClothingItem>();
        for (var i = 0; i < count; i++)
        {
            var id = packet.ReadInt();
            if (!_clothingManager.TryGetClothing(id, out var clothing) || clothing.Price <= 0)
                continue;
            if (basket.Any(item => item.Id == id) || OwnsClothing(habbo, clothing))
                continue;
            basket.Add(clothing);
        }

        void Fail(int status, string message)
        {
            session.SendNotification(message);
            session.Send(new RpBuyClothingResultComposer(status));
        }

        if (basket.Count == 0)
        {
            Fail(RpBuyClothingResultComposer.NothingToBuy, "There's nothing new on the mannequin to buy.");
            return Task.CompletedTask;
        }

        var soldOut = basket.FirstOrDefault(item => item.IsSoldOut);
        if (soldOut != null)
        {
            Fail(RpBuyClothingResultComposer.SoldOut, $"{soldOut.ShelfName} has sold out.");
            return Task.CompletedTask;
        }

        var total = basket.Sum(item => item.Price);
        if (habbo.Credits < total)
        {
            Fail(RpBuyClothingResultComposer.InsufficientCredits, $"That comes to {total} credits - you have {habbo.Credits}.");
            return Task.CompletedTask;
        }

        var tokensNeeded = basket.Count(item => item.IsLtd);
        if (tokensNeeded > 0)
        {
            var used = habbo.LoadRpInventory().Count(entry => entry.Slot >= 1 && entry.Slot <= habbo.RpUnlockedSlots);
            if (habbo.RpUnlockedSlots - used < tokensNeeded)
            {
                Fail(RpBuyClothingResultComposer.BackpackFull, tokensNeeded == 1
                    ? "A limited edition comes as a backpack token - free a slot first."
                    : $"Those {tokensNeeded} limited editions come as backpack tokens - free {tokensNeeded} slots first.");
                return Task.CompletedTask;
            }
        }

        var unlocked = new List<string>();
        var tokens = new List<string>();
        var charged = 0;
        foreach (var clothing in basket)
        {
            if (clothing.IsLtd)
            {
                // the stock-guarded claim can still lose a race for the last copy
                var edition = _clothingManager.TrySellLtd(clothing);
                if (edition == 0)
                    continue;
                if (habbo.AddRpItem($"{TokenPrefix}{clothing.Id}:{edition}") < 0)
                    continue;
                tokens.Add(clothing.ShelfName);
            }
            else
            {
                habbo.Clothing.AddClothing(clothing.ClothingName, clothing.PartIds);
                unlocked.Add(clothing.ShelfName);
            }
            charged += clothing.Price;
        }

        if (charged > 0)
        {
            habbo.Credits -= charged;
            using var connection = _database.Connection();
            connection.Execute("UPDATE `users` SET `credits` = `credits` - @amount WHERE `id` = @userId LIMIT 1", new { amount = charged, userId = habbo.Id });
            session.Send(new CreditBalanceComposer(habbo.Credits));
        }

        if (unlocked.Count > 0)
            session.Send(new FigureSetIdsComposer(habbo.Clothing.GetClothingParts));
        if (tokens.Count > 0)
            session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        session.Send(new RpClothingStoreComposer(_clothingManager.GetClothingAllParts));

        if (unlocked.Count == 0 && tokens.Count == 0)
        {
            Fail(RpBuyClothingResultComposer.SoldOut, "That sold out just now.");
            return Task.CompletedTask;
        }

        var lines = new List<string>();
        if (unlocked.Count > 0)
            lines.Add($"Bought {string.Join(", ", unlocked)} - now in Choose Your Looks.");
        if (tokens.Count > 0)
            lines.Add($"{string.Join(", ", tokens)} {(tokens.Count == 1 ? "is" : "are")} in your backpack as a token - use it to wear it.");
        session.SendNotification($"{string.Join(" ", lines)} ({charged} credits)");
        session.Send(new RpBuyClothingResultComposer(RpBuyClothingResultComposer.Ok, unlocked.Count, tokens.Count));
        return Task.CompletedTask;
    }
}
