using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Support;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: a staff member joins or leaves the rotation.
///
/// Without this the round-robin routes to whoever is away from the keyboard
/// and the chat dies there until the offer lapses - the toggle is what makes
/// turn order mean anything.
/// </summary>
internal class RpSupportAvailabilityEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var available = packet.ReadInt() == 1;
        var habbo = session.GetHabbo();
        if (habbo == null || !SupportUtility.IsStaff(habbo))
            return Task.CompletedTask;

        SupportUtility.SetAvailable(habbo.Id, available);
        SupportUtility.PushToStaff();
        return Task.CompletedTask;
    }
}
