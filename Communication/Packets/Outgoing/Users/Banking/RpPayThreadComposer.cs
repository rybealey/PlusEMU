using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Outgoing.Users.Banking;

/// <summary>
/// pixelrp Pixel Cash: everything one conversation needs to know about money.
///
/// One packet rather than three, because the client asks one question when a
/// thread opens - "can I pay this person, and what has already passed between
/// us" - and two answers that can arrive apart can disagree.
///
/// The limits travel with it so the sheet can clamp its own field. They are a
/// courtesy: PixelCash.Send checks all of them again, and its answer is the
/// one that decides.
/// </summary>
public class RpPayThreadComposer : IServerPacket
{
    private readonly int _otherUserId;
    private readonly PixelCash.Eligibility _state;
    private readonly long _remainingToday;
    private readonly List<PixelCash.Record> _records;

    public uint MessageId => ServerPacketHeader.RpPayThreadComposer;

    public RpPayThreadComposer(int otherUserId, PixelCash.Eligibility state, long remainingToday,
        List<PixelCash.Record> records)
    {
        _otherUserId = otherUserId;
        _state = state;
        _remainingToday = remainingToday;
        _records = records;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_otherUserId);
        packet.WriteInteger((int)_state);
        packet.WriteInteger(PixelCash.Minimum);
        packet.WriteInteger(PixelCash.Maximum);
        packet.WriteInteger(BankUtility.ToWire(_remainingToday));
        packet.WriteInteger(_records.Count);
        foreach (var record in _records)
        {
            packet.WriteInteger(record.Id);
            packet.WriteInteger(record.SenderId);
            packet.WriteInteger(record.RecipientId);
            packet.WriteInteger(record.Amount);
            packet.WriteString(record.Note);
            packet.WriteInteger(record.CreatedAt);
        }
    }
}
