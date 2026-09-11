using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the player chose a different character in the Wallet.
///
/// A session is bound to its Habbo at SSO and cannot be rebound, so a switch
/// is a reconnect: point the account at the target and tell the client to
/// reload. The CMS is still the only thing that mints a ticket, and on the
/// next load it mints the one for whoever `active_character_id` now names.
///
/// The check that matters is that the target is on THIS account. Without it
/// the packet is a way to point somebody else's account at a character, or to
/// enter one that is not yours.
/// </summary>
internal class RpSwitchCharacterEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var characterId = packet.ReadInt();
        if (characterId <= 0 || characterId == habbo.Id)
            return Task.CompletedTask;

        if (!AccountUtility.Characters(habbo.Id).Any(character => character.Id == characterId))
        {
            session.Send(new RpCharacterResultComposer(RpCharacterResultComposer.Refused,
                "That character is not on your account."));
            return Task.CompletedTask;
        }

        AccountUtility.SetActive(habbo.Id, characterId);
        session.Send(new RpCharacterResultComposer(RpCharacterResultComposer.Reload));
        return Task.CompletedTask;
    }
}
