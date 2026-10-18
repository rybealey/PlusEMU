using Dapper;
using Plus.Communication.Packets.Outgoing.Rooms.Action;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Authentication;

namespace Plus.HabboHotel.Users.Ignores;

internal class IgnoresEventSynchronizer : IAuthenticationTask
{
    private readonly IDatabase _database;
    private readonly IGameClientManager _gameClientManager;

    public IgnoresEventSynchronizer(IDatabase database, IGameClientManager gameClientManager)
    {
        _database = database;
        _gameClientManager = gameClientManager;
    }

    public Task UserLoggedIn(Habbo habbo)
    {
        habbo.IgnoresComponent.UserIgnored += (_, args) => Guarded(() => RegisterIgnore(habbo, args.UserId));
        habbo.IgnoresComponent.UserUnignored += (_, args) => Guarded(() => UnregisterIgnore(habbo, args.UserId));
        return Task.CompletedTask;
    }

    // pixelrp: these handlers run as async void, where nothing above them
    // catches a throw, so each one logs its own errors.
    private static async void Guarded(Func<Task> handler)
    {
        try
        {
            await handler();
        }
        catch (Exception e)
        {
            Plus.Core.ExceptionLogger.LogException(e);
        }
    }

    public async Task RegisterIgnore(Habbo habbo, int targetId)
    {
        using var connection = _database.Connection();
        await connection.ExecuteAsync("INSERT INTO user_ignores (user_id, ignore_id) VALUES (@userId, @targetId)", new { userId = habbo.Id, targetId });
        var name = await _gameClientManager.GetNameById(targetId);
        habbo.Client.Send(new IgnoreStatusComposer(1, name));
    }
    public async Task UnregisterIgnore(Habbo habbo, int targetId)
    {
        using var connection = _database.Connection();
        await connection.ExecuteAsync("DELETE FROM user_ignores WHERE user_id = @userId AND ignore_id = @targetId", new { userId = habbo.Id, targetId });
        var name = await _gameClientManager.GetNameById(targetId);
        habbo.Client.Send(new IgnoreStatusComposer(3, name));
    }
}