using Plus.Communication.Packets.Outgoing.Rooms.Engine;
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
        // Peek before consuming: a failed precondition must not burn the item.
        var item = habbo.LoadRpInventory().FirstOrDefault(candidate => candidate.Slot == slot).Item;
        if (string.IsNullOrEmpty(item))
            return Task.CompletedTask;
        switch (item)
        {
            case "smoothie":
                habbo.EnsureRpStatsLoaded();
                // Only drinkable in a safe zone, and only at full health.
                if (habbo.CurrentRoom is not { IsSafeZone: true })
                {
                    session.SendWhisper("You can only drink a Passive Smoothie in a safe zone.");
                    return Task.CompletedTask;
                }
                if (habbo.RpHealth < habbo.RpHealthMax)
                {
                    session.SendWhisper("You need full health to drink a Passive Smoothie.");
                    return Task.CompletedTask;
                }
                habbo.ConsumeRpItem(slot);
                habbo.RpPassiveSeconds = 3600;
                habbo.RpPassiveLastTick = 0;
                habbo.SaveRpStats();
                var roomUser = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
                roomUser?.OnChat(5, "*consumes the Kylie Jeener smoothie, activating passive status*", true);
                if (roomUser != null)
                    habbo.CurrentRoom.SendPacket(new RpStatsComposer(roomUser.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax, (int)Math.Round(habbo.RpAggression), 1));
                break;
        }
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        return Task.CompletedTask;
    }
}
