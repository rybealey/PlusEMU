using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the player made a new character in the Wallet.
///
/// The name is checked here and only here - the form's own hints are a
/// courtesy, and a client is free to send anything. Every way a name can fail
/// comes back as a sentence the form shows, because "invalid" tells somebody
/// naming a character nothing they can act on.
///
/// Gender picks the starting outfit, nothing more: figuredata's sets are
/// gendered, so a new character has to start in one or the other. Everything
/// about the look is theirs to change in the Avatar Editor afterwards.
/// </summary>
internal class RpCreateCharacterEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var name = (packet.ReadString() ?? "").Trim();
        var gender = (packet.ReadString() ?? "M").Trim().ToUpperInvariant();

        var characters = AccountUtility.Characters(habbo.Id);
        if (characters.Count >= AccountUtility.MaxCharacters)
        {
            session.Send(new RpCharacterResultComposer(RpCharacterResultComposer.Refused,
                "You already have three characters, and a character cannot be deleted."));
            return Task.CompletedTask;
        }

        var rejection = AccountUtility.RejectName(name);
        if (rejection != null)
        {
            session.Send(new RpCharacterResultComposer(RpCharacterResultComposer.Refused, rejection));
            return Task.CompletedTask;
        }

        var created = AccountUtility.Create(habbo.Id, name, gender);
        if (created == 0)
        {
            session.Send(new RpCharacterResultComposer(RpCharacterResultComposer.Refused,
                "That character could not be made. Try again in a moment."));
            return Task.CompletedTask;
        }

        session.Send(new RpCharactersComposer(AccountUtility.Characters(habbo.Id), habbo.Id));
        session.Send(new RpCharacterResultComposer(RpCharacterResultComposer.Created, name));
        return Task.CompletedTask;
    }
}
