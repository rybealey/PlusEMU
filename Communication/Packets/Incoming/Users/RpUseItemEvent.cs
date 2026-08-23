using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the client used a backpack item (clicked it in the Backpack).
/// Consumes one from the slot and applies the item's effect. First item:
/// the Passive Smoothie — grants one hour of online passive status,
/// announced with a bubble-5 shout.
/// </summary>
public class RpUseItemEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var slot = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || slot < 1 || slot > Plus.HabboHotel.Users.Habbo.RpCarrySlots)
            return Task.CompletedTask;
        var item = habbo.ConsumeRpItem(slot);
        if (item == null)
            return Task.CompletedTask;
        switch (item)
        {
            case "smoothie":
                habbo.EnsureRpStatsLoaded();
                habbo.RpPassiveSeconds = 3600;
                habbo.RpPassiveLastTick = 0;
                habbo.SaveRpStats();
                var roomUser = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
                roomUser?.OnChat(5, "*consume the Kylie Jeener smoothie, activating passive status*", true);
                break;
        }
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        return Task.CompletedTask;
    }
}
