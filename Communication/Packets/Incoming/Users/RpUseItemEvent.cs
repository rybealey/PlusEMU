using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.DiamondsStore;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Subscriptions;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the client used a backpack item (clicked it in the Backpack).
/// Consumes one from the slot and applies the item's effect. First item:
/// the Passive Smoothie — grants one hour of online passive status,
/// announced with a bubble-5 shout.
/// </summary>
public class RpUseItemEvent : IPacketEvent
{
    private readonly IDiamondsStoreManager _storeManager;
    private readonly IPermissionManager _permissionManager;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IBadgeManager _badgeManager;

    public RpUseItemEvent(IDiamondsStoreManager storeManager, IPermissionManager permissionManager,
        ISubscriptionManager subscriptionManager, IBadgeManager badgeManager)
    {
        _storeManager = storeManager;
        _permissionManager = permissionManager;
        _subscriptionManager = subscriptionManager;
        _badgeManager = badgeManager;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var slot = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || slot < 1 || slot > Plus.HabboHotel.Users.Habbo.RpCarrySlots)
            return;
        // Peek before consuming: a failed precondition must not burn the item.
        var item = habbo.LoadRpInventory().FirstOrDefault(candidate => candidate.Slot == slot).Item;
        if (string.IsNullOrEmpty(item))
            return;
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
                roomUser?.OnChat(5, "*consumes the Kylie Jeener smoothie, activating passive status*", true);
                if (roomUser != null)
                    habbo.CurrentRoom.SendPacket(new RpStatsComposer(roomUser.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax, (int)Math.Round(habbo.RpAggression), 1, habbo.Rank >= 5 ? 1 : 0));
                break;
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
