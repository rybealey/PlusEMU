using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Incoming.Users.Banking;

/// <summary>
/// pixelrp Pixel Cash: send it.
///
/// Thin on purpose. Every rule lives in PixelCash.Send, which the sheet's own
/// checks only mirror - so there is one place where money is allowed to move
/// and one place to read to know what is allowed.
/// </summary>
internal class RpPaySendEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var recipientId = packet.ReadInt();
        var amount = packet.ReadInt();
        var note = packet.ReadString();
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var sent = PixelCash.Send(session, recipientId, amount, note, out var message, out _);
        // Always an answer, success or not: the sheet waits for this rather
        // than closing on the tap, so a payment that did not happen never
        // looks like one that did.
        session.Send(new RpPayResultComposer(sent, message, PixelCash.RemainingToday(habbo.Id)));
        return Task.CompletedTask;
    }
}
