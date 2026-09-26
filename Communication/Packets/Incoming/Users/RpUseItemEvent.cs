using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Inventory.AvatarEffects;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.Catalog.Clothing;
using Plus.HabboHotel.DiamondsStore;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Subscriptions;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the client used a backpack item (clicked it in the Backpack).
/// Consumes one from the slot and applies the item's effect. First item:
/// the Passive Smoothie — grants one hour of online passive status,
/// announced with a bubble-5 shout.
/// </summary>
public class RpUseItemEvent : IPacketEvent
{
    private const string SpitTokenItem = "spit_token";
    private const string SpitCommandPermission = "command_spit";

    private readonly IDiamondsStoreManager _storeManager;
    private readonly IPermissionManager _permissionManager;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IBadgeManager _badgeManager;
    private readonly IClothingManager _clothingManager;

    public RpUseItemEvent(IDiamondsStoreManager storeManager, IPermissionManager permissionManager,
        ISubscriptionManager subscriptionManager, IBadgeManager badgeManager, IClothingManager clothingManager)
    {
        _storeManager = storeManager;
        _permissionManager = permissionManager;
        _subscriptionManager = subscriptionManager;
        _badgeManager = badgeManager;
        _clothingManager = clothingManager;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var slot = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || slot < 1 || slot > Plus.HabboHotel.Users.Habbo.RpCarrySlots)
            return;
        // pixelrp police: cuffs stop the things you do with your hands, and
        // rummaging in a backpack is one of them. Above the peek so nothing is
        // read, let alone spent, for somebody who cannot reach it.
        if (Plus.HabboHotel.Rooms.Chat.Commands.User.Police.PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("Your hands are cuffed.");
            return;
        }
        // Out cold, nothing comes out of the backpack either.
        if (Plus.HabboHotel.Rooms.KnockedOut.Refuse(session))
            return;
        // Peek before consuming: a failed precondition must not burn the item.
        var item = habbo.LoadRpInventory().FirstOrDefault(candidate => candidate.Slot == slot).Item;
        if (string.IsNullOrEmpty(item))
            return;
        // pixelrp Clothing Store: a limited-edition token ("clothing:<id>:<edition>")
        // unlocks its set like a bought piece. Owning it already leaves the token
        // alone (it can still be traded on); a set the shelf no longer knows is
        // left alone too rather than burned.
        if (item.StartsWith(RpBuyClothingEvent.TokenPrefix))
        {
            var fields = item.Split(':');
            if (fields.Length < 2 || !int.TryParse(fields[1], out var clothingId) || !_clothingManager.TryGetClothing(clothingId, out var tokenClothing))
            {
                session.SendWhisper("That token doesn't match anything in the store any more.");
                return;
            }
            if (RpBuyClothingEvent.OwnsClothing(habbo, tokenClothing))
            {
                session.SendWhisper($"You already own {tokenClothing.ShelfName} - keep the token or trade it on.");
                return;
            }
            habbo.ConsumeRpItem(slot);
            habbo.Clothing.AddClothing(tokenClothing.ClothingName, tokenClothing.PartIds);
            session.Send(new FigureSetIdsComposer(habbo.Clothing.GetClothingParts));
            session.SendNotification($"{tokenClothing.ShelfName} is yours to wear. Find it in Choose Your Looks.");
            session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
            return;
        }
        switch (item)
        {
            case "smoothie":
                habbo.EnsureRpStatsLoaded();
                // Only drinkable in a safe zone, and only at full health.
                if (habbo.CurrentRoom is not { IsSafeZone: true })
                {
                    session.SendWhisper("You can only drink a Passive Smoothie in a safe zone.");
                    return;
                }
                if (habbo.RpHealth < habbo.RpHealthMax)
                {
                    session.SendWhisper("You need full health to drink a Passive Smoothie.");
                    return;
                }
                habbo.ConsumeRpItem(slot);
                habbo.RpPassiveSeconds = 3600;
                habbo.RpPassiveLastTick = 0;
                habbo.SaveRpStats();
                var roomUser = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
                // blue action bubble, same as :passive and the fight commands
                roomUser?.OnChat(4, "*consumes the Kylie Jeener smoothie, activating passive status*", true);
                if (roomUser != null)
                    habbo.CurrentRoom.SendPacket(new RpStatsComposer(roomUser.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax, (int)Math.Round(habbo.RpAggression), 1, habbo.Rank >= 5 ? 1 : 0));
                // pixelrp: wear the passive enable immediately on activation.
                if (roomUser != null && habbo.Effects != null)
                    habbo.Effects.ApplyEffect(Habbo.PassiveEnableEffectId);
                break;
            // pixelrp consumables: a snack refills energy, a medkit refills
            // health, each at the whole bar over a minute. Both refuse a full
            // bar rather than burning the item for nothing, and refuse to
            // stack with themselves - a second one would not fill any faster.
            case "snack":
            {
                habbo.EnsureRpStatsLoaded();
                if (habbo.RpEnergy >= habbo.RpEnergyMax)
                {
                    session.SendWhisper("Your energy is already full.");
                    return;
                }
                if (habbo.RpEnergyRegen.Running)
                {
                    session.SendWhisper("You are already eating something.");
                    return;
                }
                habbo.ConsumeRpItem(slot);
                habbo.RpEnergyRegen.Start(habbo.RpEnergyMax);
                var snackUser = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
                if (snackUser != null)
                    snackUser.OnChat(4, "*eats a snack, slowly getting their energy back*", true);
                else
                    session.SendWhisper("You eat the snack. Your energy comes back over the next minute.");
                break;
            }
            case "medkit":
            {
                habbo.EnsureRpStatsLoaded();
                // Out cold is out of the fight: :hit refuses a target on zero
                // health for the same reason, and a knockout is undone by being
                // revived rather than by rummaging in your own backpack.
                if (habbo.RpHealth <= 0)
                {
                    session.SendWhisper("You are out cold - somebody else will have to help you.");
                    return;
                }
                if (habbo.RpHealth >= habbo.RpHealthMax)
                {
                    session.SendWhisper("You are not injured.");
                    return;
                }
                if (habbo.RpHealthRegen.Running)
                {
                    session.SendWhisper("You are already patching yourself up.");
                    return;
                }
                habbo.ConsumeRpItem(slot);
                habbo.RpHealthRegen.Start(habbo.RpHealthMax);
                var medkitUser = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
                if (medkitUser != null)
                    medkitUser.OnChat(4, "*opens a medkit and starts patching themselves up*", true);
                else
                    session.SendWhisper("You open the medkit. Stay out of trouble and you will be patched up within the minute.");
                break;
            }
            // pixelrp: Spit Token - unlocks :spit for good. Won at events or
            // spawned by staff; the command is staff-only by rank otherwise.
            // Somebody who can already spit - staff, or a second token - keeps
            // it rather than burning it for nothing.
            case SpitTokenItem:
            {
                if (habbo.Permissions.HasCommand(SpitCommandPermission))
                {
                    session.SendWhisper("You already know how to :spit - hang on to the token.");
                    return;
                }
                habbo.ConsumeRpItem(slot);
                _permissionManager.UnlockCommand(habbo.Id, SpitCommandPermission);
                habbo.Permissions = new(_permissionManager.GetPermissionsForPlayer(habbo), _permissionManager.GetCommandsForPlayer(habbo));
                var spitRoomUser = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
                spitRoomUser?.OnChat(5, "*redeems a Spit Token*", true);
                session.SendWhisper("Spit Token redeemed - you can now use :spit <username>.");
                break;
            }
            case "vip_token_31":
            case "vip_token_14":
            {
                // pixelrp: VIP token. Stacks: extending from whichever is later of
                // now / current expiry. Permissions rebuild BEFORE the badge grant
                // (GiveBadge checks required rights against the live component).
                if (!_storeManager.TryGetItem(item, out var storeItem) || storeItem.VipDays <= 0)
                    return;
                habbo.ConsumeRpItem(slot);
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                habbo.VipExpire = Math.Max(now, habbo.VipExpire) + storeItem.VipDays * 86400L;
                habbo.SaveKey("vip_expire", habbo.VipExpire.ToString());
                // pixelrp discord sync: grant the Discord VIP role promptly.
                Plus.HabboHotel.Discord.DiscordSyncUtility.Enqueue(habbo.Id, "vip");
                habbo.Permissions = new(_permissionManager.GetPermissionsForPlayer(habbo), _permissionManager.GetCommandsForPlayer(habbo));
                if (_subscriptionManager.TryGetSubscriptionData(1, out var subData) && !string.IsNullOrEmpty(subData.Badge)
                    && !habbo.Inventory.Badges.HasBadge(subData.Badge))
                    await _badgeManager.GiveBadge(habbo, subData.Badge);
                session.Send(new UserRightsComposer(2, habbo.Rank, habbo.IsAmbassador));
                session.Send(new ScrSendUserInfoComposer(habbo, 2));
                var vipRoomUser = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
                vipRoomUser?.OnChat(5, "*redeems a VIP token - VIP membership active!*", true);
                if (vipRoomUser == null)
                    session.SendWhisper($"VIP activated - {storeItem.VipDays} days added.");
                break;
            }
        }
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
    }
}
