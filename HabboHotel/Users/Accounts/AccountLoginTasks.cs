using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Users.Authentication;

namespace Plus.HabboHotel.Users.Accounts;

/// <summary>
/// pixelrp: a ban is about the person, not the character.
///
/// Without this, a second character is the ban-evasion mechanism rather than a
/// feature: ban Yavn and the same player walks back in as Marlowe a second
/// later. Every character on the account is checked, so a ban on any of them
/// locks the account out - which is what banning somebody is supposed to mean.
///
/// The per-character check still runs (BanLoginCheckTask); this is the wider
/// one, and both have to pass.
/// </summary>
internal class AccountBanCheckTask : IAuthenticationTask
{
    private readonly IModerationManager _moderationManager;

    public AccountBanCheckTask(IModerationManager moderationManager)
    {
        _moderationManager = moderationManager;
    }

    public Task<bool> CanLogin(int userId)
    {
        foreach (var character in AccountUtility.Characters(userId))
        {
            if (string.IsNullOrWhiteSpace(character.Username))
                continue;
            if (_moderationManager.IsBanned(character.Username, out _))
                return Task.FromResult(false);
            if (_moderationManager.UsernameBanCheck(character.Username))
                return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }
}

/// <summary>
/// pixelrp: one character of an account online at a time.
///
/// The stock task already disconnects a duplicate login of the SAME user; this
/// is the same rule widened to the siblings, because two characters of one
/// person in a room together is every trade, alibi and vote exploit at once.
/// The old session goes, exactly as it does when you open the client twice.
/// </summary>
internal class DisconnectSiblingCharacterTask : IAuthenticationTask
{
    private readonly IGameClientManager _gameClientManager;

    public DisconnectSiblingCharacterTask(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public Task<bool> CanLogin(int userId)
    {
        foreach (var character in AccountUtility.Characters(userId))
        {
            if (character.Id == userId)
                continue;
            _gameClientManager.GetClientByUserId(character.Id)?.Disconnect();
        }
        return Task.FromResult(true);
    }
}
