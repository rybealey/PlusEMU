using Plus.HabboHotel.Corporations;
using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Accounts;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police actions: :stun - freeze a nearby target for a few seconds.
///
/// Ported from the old Arcturus plugin. Its clocked-in-officer gate is back:
/// only an on-duty employee of a corporation flagged `is_police` can fire
/// (PoliceUtility). Its stun gun gate is back too, in the form the backpack
/// gives it: the officer must have a Stun Gun EQUIPPED in the Weapon slot
/// (RpWeapons) - carrying one is not drawing it. It holds seven shots
/// (PoliceState.StunGunShots): the last one puts it away, and a police locker
/// reloads it and equips it again.
///
/// Reach is DIRECTIONAL, which is what makes this different from every other
/// combat command in the hotel. Along a straight grid line - same row or same
/// column - it reaches two tiles, so you can stand one clear tile back. On a
/// true diagonal it reaches one. Any other offset never connects at all: a
/// knight's-move target is out of the firing line however close it looks.
///
/// A pull of the trigger is a pull of the trigger: out of reach is a public
/// miss, not a private "too far away", and it costs the cooldown and turns the
/// shooter aggressive just as a landed stun does.
/// </summary>
internal class StunCommand : ITargetChatCommand
{
    public string Key => "stun";
    public string PermissionRequired => "command_stun";

    public string Parameters => "%target%";

    public string Description => "Stun another user, freezing them briefly.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "No target selected.";

    /// <summary>Blue bubble, the one every combat action shares.</summary>
    private const int FightBubble = 4;

    /// <summary>How far a shot carries along a row or column.</summary>
    private const int StraightReach = 2;

    /// <summary>Seconds the target stays frozen. Flashbang.StunSeconds matches it.</summary>
    private const int StunSeconds = 4;

    /// <summary>
    /// Seconds between shots. The stun gun keeps its OWN timer, longer than
    /// the punching cooldown and independent of it, so neither blocks the
    /// other - as in the original.
    /// </summary>
    private const int CooldownSeconds = 4;

    private const int AggressionOnShot = 100;

    private readonly ConcurrentDictionary<int, DateTime> _lastShot = new();

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        // Police powers are a job: on the force AND clocked in.
        if (!PoliceUtility.RequireOnDuty(session, "fire a stun gun"))
            return Task.CompletedTask;

        // Seven shots, then back to a police locker (PoliceState.StunGunShots).
        // Asked FIRST, before whether the gun is equipped: running out puts it
        // away, so an officer out of stuns would otherwise be told to equip it.
        var inventory = habbo.LoadRpInventory();
        if (inventory.Any(entry => entry.Item == RpWeapons.StunGunItem) && PoliceState.StunGunShotsLeft(habbo.Id) <= 0)
        {
            session.SendWhisper("You're out of stuns.");
            return Task.CompletedTask;
        }

        // And holding one: equipped, not just carried. The refusal says which,
        // because "equip it" and "go and get one" are different errands.
        if (RpWeapons.EquippedItem(inventory) != RpWeapons.StunGunItem)
        {
            session.SendWhisper(inventory.Any(entry => entry.Item == RpWeapons.StunGunItem)
                ? "Equip your stun gun first - click it in your backpack."
                : "You need a stun gun equipped. Restock at a police locker.");
            return Task.CompletedTask;
        }

        // pixelrp: never on one of your own characters - see ChargeCommand.
        if (AccountUtility.SameAccount(habbo.Id, target.Id))
        {
            session.SendWhisper("That is one of your own characters.");
            return Task.CompletedTask;
        }
        if (target == habbo)
        {
            session.SendWhisper("You cannot stun yourself.");
            return Task.CompletedTask;
        }

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser == null)
        {
            session.SendWhisper($"{target.Username} is not in this room.");
            return Task.CompletedTask;
        }

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (thisUser == null)
            return Task.CompletedTask;

        // In someone else's custody you are in no position to shoot. Checked
        // first so an escorted player hears about the escort rather than
        // something less useful.
        if (PoliceState.IsBeingEscorted(habbo.Id))
        {
            session.SendWhisper("You cannot do that while you are being escorted.");
            return Task.CompletedTask;
        }

        habbo.EnsureRpStatsLoaded();
        target.EnsureRpStatsLoaded();

        if (habbo.IsRpPassive)
        {
            session.SendWhisper("You cannot fight while you are passive.");
            return Task.CompletedTask;
        }

        if (target.IsRpPassive)
        {
            session.SendWhisper($"{target.Username} is passive and cannot be fought.");
            return Task.CompletedTask;
        }

        // Freezing someone who is already on the floor does nothing, and the
        // stun's timed release would end up fighting the knockout over who
        // owns their ability to walk.
        if (target.RpHealth <= 0)
        {
            session.SendWhisper($"{target.Username} is already out cold.");
            return Task.CompletedTask;
        }

        // Likewise someone already in custody: the escort owns their movement
        // outright, so a stun could neither freeze them nor add anything.
        if (PoliceState.IsBeingEscorted(target.Id))
        {
            session.SendWhisper($"{target.Username} is already in custody.");
            return Task.CompletedTask;
        }

        if (_lastShot.TryGetValue(habbo.Id, out var last))
        {
            var elapsed = (DateTime.UtcNow - last).TotalSeconds;
            if (elapsed < CooldownSeconds)
            {
                var remaining = (int)Math.Ceiling(CooldownSeconds - elapsed);
                session.SendWhisper($"Cooldown [{remaining}/{CooldownSeconds}]");
                return Task.CompletedTask;
            }
        }

        // The trigger is pulled from here on, hit or miss - and a miss spends a
        // shot as surely as a hit does.
        _lastShot[habbo.Id] = DateTime.UtcNow;
        var shotsLeft = PoliceState.FireStunGun(habbo.Id);
        habbo.RpAggression = AggressionOnShot;

        if (InReach(thisUser, targetUser))
        {
            PoliceState.Stun(room, targetUser, StunSeconds);
            room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*fires a stun using their stun gun at {target.Username}, stunning them*", 0, FightBubble));
        }
        else
            room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*fires a stun using their stun gun at {target.Username} but misses*", 0, FightBubble));

        if (shotsLeft == 0)
        {
            // Out of charges, the gun is put away - the hand empties with it -
            // until a police locker reloads it, which equips it again. With a
            // full backpack there is nowhere to put it, so it stays out, empty.
            // Either way they are told, with the same words the next pull of
            // the trigger gets.
            if (RpWeapons.TryPutAway(habbo))
            {
                var after = habbo.LoadRpInventory();
                RpWeapons.ApplyToHand(habbo, after);
                session.Send(new Plus.Communication.Packets.Outgoing.Users.RpInventoryComposer(after));
            }
            session.SendWhisper("You're out of stuns.");
        }
        // The green bar on the stun gun in the backpack.
        session.Send(new Plus.Communication.Packets.Outgoing.Users.RpStunGunChargeComposer(shotsLeft, PoliceState.StunGunShots));

        PoliceState.SendStats(room, thisUser, habbo);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The firing line: straight along a row or column for two tiles, one tile
    /// on a true diagonal, nothing off those lines. The shared tile counts, as
    /// it does for a punch.
    ///
    /// Measured between the tiles the two count as on
    /// (MovementV2Bridge.ReachTiles), not RoomUser.X/Y, which for anyone
    /// mid-step still names the tile they are leaving - so an officer firing
    /// as they walked up to a target measured a tile short of where they
    /// stood, and missed. Someone mid-step counts as on the tile they are
    /// stepping onto, and on the one they are leaving for the first half of
    /// the step; the shot lands if any pairing is in line.
    /// </summary>
    internal static bool InReach(RoomUser shooter, RoomUser target)
    {
        var (shooterA, shooterB) = MovementV2Bridge.ReachTiles(shooter);
        var (targetA, targetB) = MovementV2Bridge.ReachTiles(target);
        return InLine(shooterA, targetA) || InLine(shooterA, targetB)
            || InLine(shooterB, targetA) || InLine(shooterB, targetB);
    }

    private static bool InLine(System.Drawing.Point from, System.Drawing.Point to)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = Math.Abs(to.Y - from.Y);
        if (dx == 0 && dy == 0)
            return true;
        if (dx == 0 || dy == 0)
            return Math.Max(dx, dy) <= StraightReach;
        if (dx == dy)
            return dx == 1;
        return false;
    }
}
