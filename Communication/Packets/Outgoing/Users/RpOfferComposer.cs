using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Offers;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the offer card above the buyer's chat bar - or the absence of one.
///
/// ONE PACKET FOR BOTH. `has` 0 takes the card off screen, which means the
/// client never has to work out whether something was withdrawn, expired,
/// answered or replaced by the next in the queue: it draws what it was last
/// sent and nothing else.
///
/// The blocked reason travels with it rather than being worked out client-side.
/// Whether a backpack has room is a question about rows in a table, and a
/// client that guessed would eventually guess differently from the server that
/// decides - which is the one thing a "why can I not press this" must never do.
/// </summary>
public class RpOfferComposer : IServerPacket
{
    private readonly OfferState.Offer? _offer;
    private readonly int _queued;
    private readonly string _blocked;

    public uint MessageId => ServerPacketHeader.RpOfferComposer;

    public RpOfferComposer(OfferState.Offer? offer, int queued, string? blocked)
    {
        _offer = offer;
        _queued = queued;
        _blocked = blocked ?? string.Empty;
    }

    public void Compose(IOutgoingPacket packet)
    {
        if (_offer == null)
        {
            packet.WriteInteger(0);
            return;
        }
        packet.WriteInteger(1);
        packet.WriteInteger(_offer.Id);
        packet.WriteString(_offer.SellerName);
        packet.WriteString(_offer.Key);
        packet.WriteString(_offer.Label);
        packet.WriteInteger(_offer.Quantity);
        packet.WriteInteger(_offer.Total);
        // Seconds still to run, so the rail can drain without the client and
        // the server disagreeing about when it started.
        packet.WriteInteger(Math.Max(0, (int)(_offer.ExpiresAt - DateTime.UtcNow).TotalSeconds));
        packet.WriteInteger(OfferState.LifetimeSeconds);
        packet.WriteInteger(_queued);
        packet.WriteString(_blocked);
        // Last, so everything before it reads exactly as it did: what the card
        // is asking - a sale, or :propose borrowing the card for a proposal.
        packet.WriteString(_offer.IsProposal ? "proposal" : "sale");
    }
}
