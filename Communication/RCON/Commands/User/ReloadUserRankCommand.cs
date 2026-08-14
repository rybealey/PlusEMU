using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;

namespace Plus.Communication.RCON.Commands.User;

internal class ReloadUserRankCommand : IRconCommand
{
    private readonly IDatabase _database;
    private readonly IGameClientManager _gameClientManager;
    private readonly IModerationManager _moderationManager;
    public string Description => "This command is used to reload a users rank and permissions.";

    public string Key => "reload_user_rank";
    public string Parameters => "%userId%";

    public ReloadUserRankCommand(IDatabase database, IGameClientManager gameClientManager, IModerationManager moderationManager)
    {
        _database = database;
        _gameClientManager = gameClientManager;
        _moderationManager = moderationManager;
    }

    public Task<bool> TryExecute(string[] parameters)
    {
        if (!int.TryParse(parameters[0], out var userId))
            return Task.FromResult(false);
        var client = _gameClientManager.GetClientByUserId(userId);
        if (client == null || client.GetHabbo() == null)
            return Task.FromResult(false);
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("SELECT `rank` FROM `users` WHERE `id` = @userId LIMIT 1");
            dbClient.AddParameter("userId", userId);
            client.GetHabbo().Rank = dbClient.GetInteger();
        }
        client.GetHabbo().Permissions.Init(client.GetHabbo());
        if (client.GetHabbo().Permissions.HasRight("mod_tickets"))
        {
            client.Send(new ModeratorInitComposer(
                client.GetHabbo().Id,
                _moderationManager.UserMessagePresets,
                _moderationManager.RoomMessagePresets,
                _moderationManager.GetTickets));
        }
        return Task.FromResult(true);
    }
}