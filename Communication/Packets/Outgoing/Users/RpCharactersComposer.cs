using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the characters on this player's account, for the Wallet.
///
/// Sent at login and after a create. Only ever to a session whose Habbo is in
/// that account - it is the recipient's own data, so no privacy rule applies,
/// but the ownership check is not optional and lives at every send site.
///
/// The cards need more than this (job, gang, birthday), and the Wallet asks
/// for those per character through the composers that already exist. This
/// packet carries identity and nothing else.
/// </summary>
public class RpCharactersComposer : IServerPacket
{
    private readonly List<AccountUtility.Character> _characters;
    private readonly int _currentId;

    public uint MessageId => ServerPacketHeader.RpCharactersComposer;

    public RpCharactersComposer(List<AccountUtility.Character> characters, int currentId)
    {
        _characters = characters ?? new List<AccountUtility.Character>();
        _currentId = currentId;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(AccountUtility.MaxCharacters);
        packet.WriteInteger(_currentId);
        packet.WriteInteger(_characters.Count);
        foreach (var character in _characters)
        {
            packet.WriteInteger(character.Id);
            packet.WriteString(character.Username);
            packet.WriteString(character.Look);
            packet.WriteString(character.Motto);
            packet.WriteString(character.Gender);
        }
    }
}
