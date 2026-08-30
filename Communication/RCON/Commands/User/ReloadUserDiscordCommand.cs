using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.Discord;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.RCON.Commands.User;

/// <summary>
/// Pushes the user's current Discord link status into their open client, so
/// the Settings window updates the instant the CMS finishes OAuth instead of
/// waiting for the player to reopen the page.
/// </summary>
internal class ReloadUserDiscordCommand : IRconCommand
{
    private readonly IGameClientManager _gameClientManager;

    public string Description => "This command pushes the user's Discord link status to their client.";

    public string Key => "reload_user_discord";
    public string Parameters => "%userId%";

    public ReloadUserDiscordCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public Task<bool> TryExecute(string[] parameters)
    {
        if (!int.TryParse(parameters[0], out var userId))
            return Task.FromResult(false);

        var client = _gameClientManager.GetClientByUserId(userId);

        // Offline is a normal outcome, not a failure: the client re-requests
        // status when the Discord page next opens.
        if (client == null || client.GetHabbo() == null)
            return Task.FromResult(true);

        var state = DiscordSyncUtility.GetLinkState(userId);
        client.Send(new RpDiscordStatusComposer(!string.IsNullOrEmpty(state.DiscordId), state.DiscordLinkedAt));

        return Task.FromResult(true);
    }
}
