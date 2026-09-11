using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Incoming.Users.Banking;

/// <summary>
/// pixelrp: the player ejected their card, or closed the ATM window.
///
/// Ending the session here rather than waiting for it to lapse means walking
/// away and coming back opens a fresh machine, and a closed window is not a
/// permission that quietly outlives it.
/// </summary>
internal class RpCloseAtmEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        AtmSessions.End(habbo.Id);
        return Task.CompletedTask;
    }
}
