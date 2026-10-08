using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police: pepper spray - one can, one spray, aimed at one person.
///
/// Sprayed with :ps (user), or by clicking the can in the backpack with a
/// target selected - the client turns that click into the same :ps. Both come
/// here, to <see cref="Spray"/>, which holds every rule.
///
/// Reach is the stun gun's firing line (StunCommand.InReach): two tiles along
/// a row or column, one on a diagonal, measured where the two are on screen.
/// The can is SPENT on the spray, hit or miss, like a flashbang on the throw;
/// the Police Replenish locker hands an officer with none a new one. No
/// cooldown: the supply is the limit.
///
/// A hit DISORIENTS: for four seconds the target can neither walk nor fight,
/// and wears the stun's birds (PoliceState.Disorient). They stumble
/// <see cref="StumbleBackSteps"/> tiles back from the officer, still facing
/// them, then <see cref="StumbleRandomSteps"/> steps in random directions
/// (MovementV2Bridge.Stumble). A wall or furni in the way cuts it short. Anybody in an escort is let go first - a suspect sprayed out of an
/// officer's hands stumbles off, and an officer sprayed mid-escort lets go of
/// theirs - because a stumble and an escort cannot both own one body.
/// </summary>
public static class PepperSpray
{
    /// <summary>The backpack item key.</summary>
    public const string Item = "pepper_spray";

    /// <summary>Tiles a hit sends the target stumbling back, facing the officer.</summary>
    private const int StumbleBackSteps = 2;

    /// <summary>Steps in random directions after that, facing where they go.</summary>
    private const int StumbleRandomSteps = 3;

    /// <summary>
    /// How long a hit disorients - no walking, no fighting: four seconds, the
    /// same as a stun. The stumble's five steps - two back, three at random -
    /// take about half of that.
    /// </summary>
    private const int DisorientMs = 4000;

    /// <summary>The blue fight bubble :stun and :cuff use.</summary>
    private const int FightBubble = 4;

    /// <summary>What a spray sets the officer's aggression to - :stun's figure.</summary>
    private const int AggressionOnSpray = 100;

    public static void Spray(GameClient session, Room room, Habbo target)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || room == null || target == null)
            return;
        if (!PoliceUtility.RequireOnDuty(session, "use pepper spray"))
            return;
        // The backpack click reaches here as a typed :ps, so CommandManager's
        // cuffed / disoriented / out-cold gates have already run. Asked again
        // anyway: this is the one place every rule lives.
        if (KnockedOut.Refuse(session))
            return;
        if (PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("Your hands are cuffed.");
            return;
        }
        if (PoliceState.IsDisoriented(habbo.Id))
        {
            session.SendWhisper("You are too disoriented to do that.");
            return;
        }

        var slot = habbo.LoadRpInventory().FirstOrDefault(entry => entry.Item == Item).Slot;
        if (slot <= 0)
        {
            session.SendWhisper("You don't have any pepper spray. Restock at a police locker.");
            return;
        }

        // pixelrp: never on one of your own characters - see ChargeCommand.
        if (AccountUtility.SameAccount(habbo.Id, target.Id))
        {
            session.SendWhisper("That is one of your own characters.");
            return;
        }
        if (target == habbo)
        {
            session.SendWhisper("You cannot pepper spray yourself.");
            return;
        }

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser == null)
        {
            session.SendWhisper(RangeMessages.NotInRoom);
            return;
        }
        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (thisUser == null)
            return;

        if (PoliceState.IsBeingEscorted(habbo.Id))
        {
            session.SendWhisper("You cannot do that while you are being escorted.");
            return;
        }

        habbo.EnsureRpStatsLoaded();
        target.EnsureRpStatsLoaded();
        if (habbo.IsRpPassive)
        {
            session.SendWhisper("You cannot fight while you are passive.");
            return;
        }
        if (target.IsRpPassive)
        {
            session.SendWhisper($"{target.Username} is passive and cannot be fought.");
            return;
        }
        if (target.RpHealth <= 0)
        {
            session.SendWhisper($"{target.Username} is already out cold.");
            return;
        }
        // Not on a fellow officer at work - as a flashbang never catches one.
        // Last, because it is the one check that asks the database.
        if (PoliceUtility.IsOnDutyOfficer(target.Id))
        {
            session.SendWhisper($"{target.Username} is an on-duty police officer.");
            return;
        }

        // Sprayed from here on, hit or miss: the can is spent either way.
        habbo.ConsumeRpItem(slot);
        session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        habbo.RpAggression = AggressionOnSpray;

        if (StunCommand.InReach(thisUser, targetUser))
        {
            // Which way is "back": directly away from the officer, from where
            // the two are on screen. On a shared tile there is no away, so it
            // is the way the officer faces.
            var from = MovementV2Bridge.ReachTiles(thisUser).Second;
            var at = MovementV2Bridge.ReachTiles(targetUser).Second;
            var awayX = Math.Sign(at.X - from.X);
            var awayY = Math.Sign(at.Y - from.Y);
            if (awayX == 0 && awayY == 0)
            {
                var ahead = MovementController.FacingDelta((byte)thisUser.RotBody);
                awayX = ahead.X;
                awayY = ahead.Y;
            }
            // Facing back at the officer the whole way.
            var facing = (byte)Rotation.Calculate(0, 0, -awayX, -awayY);

            // Out of any escort first, either side of it: a stumble and an
            // escort cannot both move one body.
            var captorId = PoliceState.CaptorOf(target.Id);
            if (captorId != 0)
                PoliceState.EndEscort(room, captorId, targetUser);
            if (PoliceState.IsEscorting(target.Id))
                PoliceState.EndEscort(room, target.Id, null);
            // The spray takes over from a stun, as a cuff does. Disoriented
            // FIRST, so ending the stun leaves them unable to walk and keeps
            // the birds on rather than flicking them off and on again.
            PoliceState.Disorient(targetUser, DisorientMs);
            PoliceState.CancelStun(targetUser);
            MovementV2Bridge.Stumble(targetUser, awayX, awayY, StumbleBackSteps, StumbleRandomSteps, facing);
            room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*sprays their pepper spray at {target.Username}, disorienting them*", 0, FightBubble));
        }
        else
            room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*sprays their pepper spray at {target.Username} but misses*", 0, FightBubble));

        PoliceState.SendStats(room, thisUser, habbo);
    }
}
