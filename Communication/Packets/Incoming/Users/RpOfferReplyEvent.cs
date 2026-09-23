using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Offers;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the buyer answered the card. Thin on purpose - every rule lives in
/// OfferState.Accept, which re-checks all of it, so this cannot be the place a
/// check is forgotten.
/// </summary>
internal class RpOfferReplyEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var offerId = packet.ReadInt();
        var accepted = packet.ReadInt() == 1;
        if (session.GetHabbo() == null || offerId <= 0)
            return Task.CompletedTask;

        if (accepted)
            OfferState.Accept(offerId, session);
        else
            OfferState.Decline(offerId, session.GetHabbo());
        return Task.CompletedTask;
    }
}
