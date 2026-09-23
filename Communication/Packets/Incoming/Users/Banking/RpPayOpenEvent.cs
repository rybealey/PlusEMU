using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Incoming.Users.Banking;

/// <summary>
/// pixelrp Pixel Cash: a conversation was opened - what does money look like
/// in it?
///
/// The client cannot answer this for itself. It knows whether IT banks
/// anywhere, and nothing at all about whether the other person does; that
/// second fact is the whole reason this packet exists.
/// </summary>
internal class RpPayOpenEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var otherUserId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || otherUserId <= 0)
            return Task.CompletedTask;

        var state = PixelCash.Check(habbo.Id, otherUserId);
        // The history is sent whatever the state says. A pair who used to be
        // able to pay each other and now cannot - somebody closed an account -
        // still has a conversation with receipts in it, and those receipts do
        // not stop being true.
        session.Send(new RpPayThreadComposer(otherUserId, state,
            PixelCash.RemainingToday(habbo.Id), PixelCash.Between(habbo.Id, otherUserId)));
        return Task.CompletedTask;
    }
}
