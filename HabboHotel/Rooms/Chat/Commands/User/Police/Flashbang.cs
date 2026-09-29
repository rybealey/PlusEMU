using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police: the flashbang - :stun for everyone at arm's length at once.
///
/// Thrown from the backpack (click it) or with :fb, both through
/// <see cref="Throw"/>. It stuns every player on the officer's tile or on one
/// of the eight around it - the full 3x3 block, diagonals in - exactly as a
/// stun gun hit does: the same freeze and the same length. Out of its reach:
/// the passive, anyone already out cold and the officer's own characters. A
/// prisoner in custody IS caught - unlike a stun gun shot, a bang does not pick
/// its targets - and shows the stun's birds over their cuffs for the freeze
/// (PoliceState.TickCuffs yields while stunned; the escort carries on). The
/// officer is never caught in their own bang, and nor is any other ON-DUTY
/// officer - a squad clearing a room together does not stun itself. Off duty,
/// an officer is anyone else.
///
/// It is SPENT on the throw, whether it catches anyone or not - a flashbang
/// thrown into an empty corner is still gone. The Police Replenish locker
/// hands an officer with none a new one. No stun gun and no cooldown: the
/// supply is the limit.
/// </summary>
public static class Flashbang
{
    /// <summary>The backpack item key.</summary>
    public const string Item = "flashbang";

    /// <summary>The blue fight bubble :stun and :cuff use.</summary>
    private const int FightBubble = 4;

    /// <summary>How far it reaches: one tile, in every direction, the officer's own included.</summary>
    private const int Reach = 1;

    /// <summary>The freeze, matched to :stun's (StunCommand.StunSeconds).</summary>
    private const int StunSeconds = 4;

    /// <summary>What throwing one sets the officer's aggression to - :stun's figure.</summary>
    private const int AggressionOnThrow = 100;

    public static void Throw(GameClient session, Room room)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null)
            return;
        if (room == null)
        {
            session.SendWhisper("You need to be in a room to throw a flashbang.");
            return;
        }
        if (!PoliceUtility.RequireOnDuty(session, "throw a flashbang"))
            return;
        if (KnockedOut.Refuse(session))
            return;
        if (PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("Your hands are cuffed.");
            return;
        }
        // The backpack click comes here without passing CommandManager's gate.
        if (PoliceState.IsDisoriented(habbo.Id))
        {
            session.SendWhisper("You are too disoriented to do that.");
            return;
        }
        if (PoliceState.IsBeingEscorted(habbo.Id))
        {
            session.SendWhisper("You cannot do that while you are being escorted.");
            return;
        }

        habbo.EnsureRpStatsLoaded();
        if (habbo.IsRpPassive)
        {
            session.SendWhisper("You cannot fight while you are passive.");
            return;
        }

        var slot = habbo.LoadRpInventory().FirstOrDefault(entry => entry.Item == Item).Slot;
        if (slot <= 0)
        {
            session.SendWhisper("You don't have a flashbang. Restock at a police locker.");
            return;
        }

        var thrower = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (thrower == null)
            return;

        // Everyone in the block, then the ones a stun gun could not touch left out.
        var caught = new List<RoomUser>();
        foreach (var user in room.GetRoomUserManager().GetRoomUsers().ToList())
        {
            var target = user?.GetClient()?.GetHabbo();
            if (user == null || user.IsBot || target == null || target.Id == habbo.Id)
                continue;
            if (Math.Max(Math.Abs(user.X - thrower.X), Math.Abs(user.Y - thrower.Y)) > Reach)
                continue;
            if (AccountUtility.SameAccount(habbo.Id, target.Id))
                continue;
            target.EnsureRpStatsLoaded();
            // A prisoner in custody is caught too (see the summary above).
            if (target.IsRpPassive || target.RpHealth <= 0)
                continue;
            // Last, because it is the one check that asks the database.
            if (PoliceUtility.IsOnDutyOfficer(target.Id))
                continue;
            caught.Add(user);
        }

        habbo.ConsumeRpItem(slot);
        habbo.RpAggression = AggressionOnThrow;
        foreach (var user in caught)
            PoliceState.Stun(room, user, StunSeconds);

        // One bubble whoever it catches: the freeze on each of them says the rest.
        room.SendPacket(new ChatComposer(thrower.VirtualId, "*throws a flashbang*", 0, FightBubble));

        PoliceState.SendStats(room, thrower, habbo);
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
    }
}
