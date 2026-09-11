using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: an officer clicked the x beside one charge in the Wanted list's
/// rap sheet.
///
/// ONE count, not the crime: a sheet carrying three counts of assault loses
/// one of them per click, oldest first, and the tooltip's "x3" ticks down. The
/// whole-sheet clearance is :pardon; this is the officer correcting a sheet
/// line by line, which is the thing :pardon deliberately cannot do.
///
/// Gated on the same rule as every police power - employed by a force and
/// clocked in - re-checked here rather than trusted from the client, which is
/// free to send anything.
///
/// There is no same-room requirement - a sheet is worked from a list, and the
/// person it belongs to may be anywhere - but it is still announced, in the
/// officer's own room, like every other police action. Dropping a charge is a
/// thing an officer DID, and the room they are standing in is where the
/// roleplay for it happens. The tally is not in the bubble: the tooltip's x2
/// ticks down in front of the officer as they click, which is the feedback.
/// </summary>
internal class RpDropChargeEvent : IPacketEvent
{
    /// <summary>Blue bubble, the one every police action shares.</summary>
    private const int PoliceBubble = 4;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var userId = packet.ReadInt();
        var crimeId = packet.ReadInt();
        if (userId <= 0 || crimeId <= 0)
            return Task.CompletedTask;

        if (!PoliceUtility.RequireOnDuty(session, "drop a charge"))
            return Task.CompletedTask;

        // Never your own character's sheet - see ChargeCommand. This is the
        // quiet one: no room sees it, so it is the easiest to try.
        if (AccountUtility.SameAccount(habbo.Id, userId))
        {
            session.SendWhisper("That is one of your own characters.");
            return Task.CompletedTask;
        }

        // A sheet that has already lapsed has nothing to drop, and its rows
        // must not be droppable one by one after the fact.
        WantedUtility.ExpireLapsed();

        var dropped = WantedUtility.DropOneCharge(userId, crimeId);
        if (dropped == null)
        {
            session.SendWhisper("That charge is no longer on their record.");
            return Task.CompletedTask;
        }

        // Narrated: the client moves the opening "*" ahead of the officer's
        // name, so this reads "*Yavn drops one count of Assault against
        // twist*". No room, no bubble - an officer working a sheet from the
        // hotel view still drops the charge, they just do it unwitnessed.
        var officerUser = habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (officerUser != null)
            habbo.CurrentRoom.SendPacket(new ChatComposer(officerUser.VirtualId, dropped.Remaining > 0
                ? $"*drops one count of {dropped.CrimeName} against {dropped.Username}*"
                : $"*drops {dropped.CrimeName} against {dropped.Username}*", 0, PoliceBubble));

        WantedUtility.Broadcast();
        return Task.CompletedTask;
    }
}
