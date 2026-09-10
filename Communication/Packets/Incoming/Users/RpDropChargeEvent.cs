using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

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
/// free to send anything. There is no same-room requirement: this is paperwork
/// done from a list, not a public act in front of a room, so it is also the
/// one police action that announces nothing. The officer gets a whisper and
/// everybody's wanted list updates; nobody gets a bubble.
/// </summary>
internal class RpDropChargeEvent : IPacketEvent
{
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

        // A sheet that has already lapsed has nothing to drop, and its rows
        // must not be droppable one by one after the fact.
        WantedUtility.ExpireLapsed();

        var dropped = WantedUtility.DropOneCharge(userId, crimeId);
        if (dropped == null)
        {
            session.SendWhisper("That charge is no longer on their record.");
            return Task.CompletedTask;
        }

        session.SendWhisper(dropped.Remaining > 0
            ? $"Dropped one count of {dropped.CrimeName} against {dropped.Username}. {dropped.Remaining} still stand."
            : $"Dropped {dropped.CrimeName} against {dropped.Username}.");

        WantedUtility.Broadcast();
        return Task.CompletedTask;
    }
}
