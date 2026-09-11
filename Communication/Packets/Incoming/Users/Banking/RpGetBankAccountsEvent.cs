using Plus.Communication.Packets.Outgoing.Users.Banking;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Banking;

namespace Plus.Communication.Packets.Incoming.Users.Banking;

/// <summary>
/// pixelrp: the Wallet asked for this character's accounts.
///
/// Answered from the cache, which login already filled - the Wallet is opened
/// far more often than an account changes, and a screen that queries on every
/// open is a query per player per open for a number that has not moved.
/// </summary>
internal class RpGetBankAccountsEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        session.Send(new RpBankAccountsComposer(BankUtility.Get(habbo.Id)));
        return Task.CompletedTask;
    }
}
