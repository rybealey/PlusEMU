using Dapper;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.UserData;
using System.Diagnostics;

namespace Plus.HabboHotel.Users.Authentication;

internal class Authenticator : IAuthenticator
{
    private readonly IEnumerable<IAuthenticationTask> _authenticationTasks;
    private readonly IGameClientManager _gameClientManager;
    private readonly IUserDataFactory _userDataFactory;
    private readonly IDatabase _database;

    public Authenticator(IEnumerable<IAuthenticationTask> authenticationTasks, IGameClientManager gameClientManager, IUserDataFactory userDataFactory, IDatabase database)
    {
        _authenticationTasks = authenticationTasks;
        _gameClientManager = gameClientManager;
        _userDataFactory = userDataFactory;
        _database = database;
    }

    public async Task<AuthenticationError?> AuthenticateUsingSSO(GameClient session, string sso)
    {
        sso = sso.Trim();
        if (string.IsNullOrEmpty(sso))
            return AuthenticationError.EmptySSO;

        if (!Debugger.IsAttached && sso.Length < 15)
            return AuthenticationError.InvalidSSO;

        var userId = await GetUserIdFromSso(sso);
        if (userId == default)
            return AuthenticationError.NoAccountFound;

        if (!Debugger.IsAttached)
            await ResetSso(userId);

        var canLogin = await CanLogin(userId);
        if (!canLogin)
            return AuthenticationError.LoginProhibited;

        var habbo = await _userDataFactory.Create(userId);
        if (habbo == null)
            return AuthenticationError.NoAccountFound;

        habbo.Disconnected += async (_, _) => await OnHabboDisconnected(habbo);

        session.SetHabbo(habbo);
        // pixelrp: the connection can close during the awaits above - a
        // refresh on the loading screen, or PacketManager giving up on a slow
        // login after 5 seconds - and then GameClient.OnDisconnected found no
        // Habbo to log out. Carrying on registered a player nobody would ever
        // log out: online for good, and kept in memory by its process timer.
        // So stop here, before anything is registered: there is nothing to
        // undo. (Closed after SetHabbo, OnDisconnected saw this Habbo and is
        // logging it out itself.)
        if (session.IsDisconnected)
            return AuthenticationError.ConnectionClosed;

        // TODO @80O: Remove after splitting up
        habbo.Init(session);
        _gameClientManager.RegisterClient(session, habbo.Id, habbo.Username);
        // Nothing ever set this flag on login (only the disconnect/boot paths
        // clear it), so users.online — and everything built on it, like the
        // CMS online counter — read 0 regardless of live connections.
        await MarkOnline(habbo.Id);
        await RaiseHabboLoggedIn(habbo);
        // pixelrp: closed since SetHabbo, so OnDisconnected is logging this
        // Habbo out on its own thread - and that can finish before the three
        // steps above, which then put a player who has gone back on the
        // register, online, and into their friends' lists. Undo them, unless a
        // newer session of the same player already holds the id (it owns all
        // three then), and stop the login here.
        if (session.IsDisconnected)
        {
            var current = _gameClientManager.GetClientByUserId(habbo.Id);
            if (current == null || current == session)
            {
                if (current == session)
                    _gameClientManager.UnregisterClient(habbo.Id, habbo.Username);
                await MarkOffline(habbo.Id);
                await OnHabboDisconnected(habbo);
            }
            return AuthenticationError.ConnectionClosed;
        }
        return null;
    }

    private async Task MarkOnline(int userId)
    {
        using var connection = _database.Connection();
        await connection.ExecuteAsync("UPDATE users SET online = true WHERE id = @userId", new { userId });
    }

    private async Task MarkOffline(int userId)
    {
        using var connection = _database.Connection();
        await connection.ExecuteAsync("UPDATE users SET online = false WHERE id = @userId", new { userId });
    }

    private async Task<int> GetUserIdFromSso(string sso)
    {
        using var connection = _database.Connection();
        return await connection.ExecuteScalarAsync<int>("SELECT id FROM users WHERE auth_ticket = @sso", new { sso });
    }

    private async Task ResetSso(int userId)
    {
        using var connection = _database.Connection();
        await connection.ExecuteAsync("UPDATE users SET auth_ticket = NULL WHERE id = @userId", new { userId });
    }

    private async Task<bool> CanLogin(int userId)
    {
        foreach (var task in _authenticationTasks)
        {
            if (!(await task.CanLogin(userId)))
                return false;
        }
        return true;
    }

    private async Task RaiseHabboLoggedIn(Habbo habbo)
    {
        foreach (var task in _authenticationTasks)
            await task.UserLoggedIn(habbo);
    }

    private async Task OnHabboDisconnected(Habbo habbo)
    {
        // pixelrp: each task guarded on its own. This runs from an async void
        // event handler (Habbo.Disconnected above), where nothing catches a
        // throw - it would take the whole emulator down - and one failing task
        // must not stop the others (friends still need telling you left).
        foreach (var task in _authenticationTasks)
        {
            try
            {
                await task.UserLoggedOut(habbo);
            }
            catch (Exception e)
            {
                Plus.Core.ExceptionLogger.LogException(e);
            }
        }
    }
}