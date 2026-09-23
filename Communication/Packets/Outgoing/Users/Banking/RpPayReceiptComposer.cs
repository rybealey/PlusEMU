using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Outgoing.Users.Banking;

/// <summary>
/// pixelrp Pixel Cash: one payment, live, to both ends of it.
///
/// Both ends get the SAME packet - it carries who paid and who was paid, and
/// each client works out which side of it they are on. A per-viewer "incoming"
/// flag would be one more thing that can be composed the wrong way round, and
/// the direction is already implied by two ids the client has.
/// </summary>
public class RpPayReceiptComposer : IServerPacket
{
    private readonly PixelCash.Record _record;

    public uint MessageId => ServerPacketHeader.RpPayReceiptComposer;

    public RpPayReceiptComposer(PixelCash.Record record)
    {
        _record = record;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_record.Id);
        packet.WriteInteger(_record.SenderId);
        packet.WriteInteger(_record.RecipientId);
        packet.WriteInteger(_record.Amount);
        packet.WriteString(_record.Note);
        packet.WriteInteger(_record.CreatedAt);
    }
}
