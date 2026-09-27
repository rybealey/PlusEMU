using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// pixelrp: the police equipment locker.
///
/// An on-duty officer double-clicks it and is TOPPED UP: a pair of handcuffs
/// if they carry none, a stun gun if they carry none. What they already have
/// is left alone, so a second click says they are fully equipped and hands
/// out nothing - which is also why there is no cooldown. It only ever fills a
/// gap, so there is nothing to farm.
///
/// The stun gun counts wherever it is: in the backpack or equipped in the
/// Weapon slot. A new one goes in the backpack, not into the hand - equipping
/// stays the officer's own act.
///
/// Walk-up and facing are the ATM's (InteractorAtm): used from across the
/// room, the officer walks over first.
/// </summary>
public class InteractorPoliceReplenish : IFurniInteractor
{
    /// <summary>The police / fight bubble, the one :cuff and :stun use.</summary>
    private const int ActionBubble = 4;

    public void OnTrigger(GameClient session, Item item, int request, bool hasRights)
    {
        if (session == null || item == null)
            return;
        var habbo = session.GetHabbo();
        if (habbo == null)
            return;
        var room = item.GetRoom();
        if (room == null)
            return;
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (user == null)
            return;

        if (!Gamemap.TilesTouching(user.X, user.Y, item.GetX, item.GetY))
        {
            user.MoveTo(item.SquareInFront);
            return;
        }

        user.SetRot(Rotation.Calculate(user.X, user.Y, item.GetX, item.GetY), false);

        if (PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("Your hands are cuffed.");
            return;
        }

        if (!PoliceUtility.RequireOnDuty(session, "restock police equipment"))
            return;

        var inventory = habbo.LoadRpInventory();
        var missing = new List<(string Item, string Name)>();
        if (!inventory.Any(entry => entry.Item == CuffCommand.HandcuffsItem))
            missing.Add((CuffCommand.HandcuffsItem, "handcuffs"));
        if (!inventory.Any(entry => entry.Item == RpWeapons.StunGunItem))
            missing.Add((RpWeapons.StunGunItem, "stun gun"));

        if (missing.Count == 0)
        {
            session.SendWhisper("You're already fully equipped.");
            return;
        }

        // Each one on its own: a full backpack that has room for one of the two
        // still gets that one, and is told plainly which did not fit.
        var given = new List<string>();
        foreach (var (key, name) in missing)
        {
            if (habbo.AddRpItem(key) == -1)
                session.SendWhisper($"Your backpack is full - there was no room for the {name}.");
            else
                given.Add(name);
        }

        if (given.Count == 0)
            return;

        room.SendPacket(new ChatComposer(user.VirtualId, $"*restocks their {string.Join(" and ", given)}*", 0, ActionBubble));
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
    }
}
