using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Incoming.Users.Banking;

/// <summary>
/// pixelrp: Mercury asked for this character's ledger.
///
/// No payload and no paging. The app asks when it opens and again after
/// anything moves, and what comes back is what the screen holds - a cap rather
/// than a cursor, because a player scrolling two days back is looking for one
/// movement they remember, not reading an archive.
///
/// Read from the table, not the cache: the ledger is the one part of banking
/// where being a few seconds stale would actually be wrong, since checking it
/// is usually how somebody confirms the thing they just did.
/// </summary>
internal class RpGetBankLedgerEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        session.Send(new RpBankLedgerComposer(BankUtility.Ledger(habbo.Id, BankUtility.MaxLedgerRows)));
        return Task.CompletedTask;
    }
}
