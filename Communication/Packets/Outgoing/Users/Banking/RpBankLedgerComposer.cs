using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Outgoing.Users.Banking;

/// <summary>
/// pixelrp: a character's own ledger, for the Mercury app.
///
/// BOTH accounts travel in one message. The app switches between checking and
/// savings with a segmented control and filters by kind with chips, and every
/// one of those is an instant, local decision - asking the server again for
/// each would make a toggle feel like a page load.
///
/// Only ever the recipient's own. Nothing here carries a user id, because
/// there is no case where a player is shown somebody else's ledger.
/// </summary>
public class RpBankLedgerComposer : IServerPacket
{
    private readonly IReadOnlyList<BankTransaction> _rows;

    public uint MessageId => ServerPacketHeader.RpBankLedgerComposer;

    public RpBankLedgerComposer(IReadOnlyList<BankTransaction> rows)
    {
        _rows = rows ?? new List<BankTransaction>();
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_rows.Count);

        foreach (var row in _rows)
        {
            packet.WriteInteger(BankUtility.ToWireSigned(row.Id));
            packet.WriteString(row.Kind ?? string.Empty);
            packet.WriteString(row.Account ?? string.Empty);
            // Signed: negative is money leaving that account. ToWire would
            // floor it at zero and turn every withdrawal into a 0.
            packet.WriteInteger(BankUtility.ToWireSigned(row.Amount));
            packet.WriteInteger(BankUtility.ToWire(row.BalanceAfter));
            packet.WriteString(row.Source ?? string.Empty);
            packet.WriteInteger(row.CreatedAt);
        }
    }
}
