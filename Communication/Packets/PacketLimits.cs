using Plus.Communication.Packets.Incoming.Camera;
using Plus.Communication.Packets.Incoming.Catalog;
using Plus.Communication.Packets.Incoming.Groups;
using Plus.Communication.Packets.Incoming.Inventory.Trading;
using Plus.Communication.Packets.Incoming.Marketplace;
using Plus.Communication.Packets.Incoming.Rooms.AI.Pets;
using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.Communication.Packets.Incoming.Rooms.Chat;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Incoming.Users;
using Plus.Communication.Packets.Incoming.Users.Banking;

namespace Plus.Communication.Packets;

/// <summary>How often one connection may send a packet: a token bucket holding
/// <see cref="Burst"/> at most, refilled at <see cref="PerSecond"/>. A limit that
/// is not <see cref="Shared"/> has its bucket only - it takes nothing from
/// <see cref="PacketLimits.Total"/> and its drops never count towards a flood.</summary>
public readonly record struct PacketLimit(double PerSecond, int Burst, bool Shared = true);

/// <summary>
/// pixelrp: every packet limit in one place (PacketBudget applies them; the
/// PacketManager drops what goes over).
///
/// A person clicking flat out manages 5-10 of one action a second, and nothing
/// the client sends on its own repeats faster than about 8 - the only big
/// bursts are log in and room entry, many DIFFERENT packets at once, which the
/// bursts cover. Tighter limits sit on what a bot gains from doing fast: chat
/// (commands included), buying, and moving money.
///
/// Walking is the movement system's and is never limited or counted here.
/// </summary>
internal static class PacketLimits
{
    /// <summary>Any one kind of packet not listed below.</summary>
    public static readonly PacketLimit Default = new(10, 40);

    /// <summary>Every packet of a connection together, whatever its kind.</summary>
    public static readonly PacketLimit Total = new(20, 150);

    /// <summary>Talking, shouting, whispering - and every :command, which arrives as chat.</summary>
    public static readonly PacketLimit Chat = new(4, 8);

    /// <summary>Anything that spends currency, and voucher codes (which also stops guessing them).</summary>
    public static readonly PacketLimit Buying = new(2, 4);

    /// <summary>Moving money - ATM, bank, Pay - and agreeing to a trade or listing an offer.</summary>
    public static readonly PacketLimit Money = new(3, 6);

    /// <summary>Each catalog search scans the whole catalog; the client waits 300ms after typing (CatalogSearchView), so 3 a second at most.</summary>
    public static readonly PacketLimit Search = new(4, 8);

    /// <summary>What a click on a player, pet or group furni asks for - turning to look, their badges,
    /// partner, gang and group, and who the HUD targets. Clicking from player to player sends four or
    /// five a click, which took a fast clicker past the total and had them dropped unseen (a turn that
    /// never came, a HUD target the server never heard) and, kept up, disconnected for flooding.</summary>
    public static readonly PacketLimit Selection = new(10, 40, Shared: false);

    private static readonly Dictionary<Type, PacketLimit> Listed = new()
    {
        [typeof(ChatEvent)] = Chat,
        [typeof(ShoutEvent)] = Chat,
        [typeof(WhisperEvent)] = Chat,

        [typeof(PurchaseFromCatalogEvent)] = Buying,
        [typeof(PurchaseFromCatalogAsGiftEvent)] = Buying,
        [typeof(PurchaseDiamondsStoreItemEvent)] = Buying,
        [typeof(PurchasePhotoEvent)] = Buying,
        [typeof(PurchaseGroupEvent)] = Buying,
        [typeof(PurchaseTargetedOfferEvent)] = Buying,
        [typeof(PurchaseVipMembershipExtensionEvent)] = Buying,
        [typeof(PurchaseBasicMembershipExtensionEvent)] = Buying,
        [typeof(RpBuyClothingEvent)] = Buying,
        [typeof(RpBuyGangEvent)] = Buying,
        [typeof(BuyOfferEvent)] = Buying,
        [typeof(RedeemVoucherEvent)] = Buying,

        // RP trades (RpTradeActionEvent) keep the default: RpTrades has its
        // own 100ms gap, and items are added to a trade one quick click at a time.
        [typeof(RpAtmTransactionEvent)] = Money,
        [typeof(RpBankTransferEvent)] = Money,
        [typeof(RpPaySendEvent)] = Money,
        [typeof(MakeOfferEvent)] = Money,
        [typeof(TradingAcceptEvent)] = Money,
        [typeof(TradingConfirmEvent)] = Money,

        [typeof(RpCatalogSearchEvent)] = Search,
        [typeof(LookToEvent)] = Selection,
        [typeof(GetSelectedBadgesEvent)] = Selection,
        [typeof(GetRelationshipsEvent)] = Selection,
        [typeof(RpGetUserGangEvent)] = Selection,
        [typeof(RpSetHudTargetEvent)] = Selection,
        [typeof(GetGroupInfoEvent)] = Selection,
        [typeof(GetPetInformationEvent)] = Selection,
    };

    /// <summary>The limit for a packet type, or null when it is never limited (walking).</summary>
    public static PacketLimit? For(Type packet) =>
        packet == typeof(MoveAvatarEvent) ? null : Listed.GetValueOrDefault(packet, Default);
}
