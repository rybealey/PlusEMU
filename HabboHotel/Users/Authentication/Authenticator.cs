using System.Collections.Concurrent;
using Dapper;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Accounts;
using Plus.HabboHotel.Users.UserData;
using System.Diagnostics;
using System.Text.RegularExpressions;

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

        // pixelrp: a ticket from /game carries the time the CMS issued it
        // (User::ssoTicket) and is refused once TicketLifetimeSeconds have
        // passed, so one taken from the page and kept for later - by a bot, or
        // past a browser guard pause - is soon worthless. Generous enough for a
        // slow first load of the client. Tickets without the time (the bot's own
        // logins, bot/src/sso.ts) are not timed.
        if (!Debugger.IsAttached && IsExpired(sso))
            return AuthenticationError.InvalidSSO;

        // pixelrp: look up and clear the ticket in ONE step (ConsumeSso). Two
        // connections arriving with the same ticket used to both pass a
        // separate SELECT before either cleared it, giving one player two live
        // sessions - each with its own copy of the wallet. The row lock now
        // lets exactly one through; the other finds the ticket already gone.
        // A debugger keeps the old reuse-the-ticket behaviour for dev.
        var userId = Debugger.IsAttached ? await GetUserIdFromSso(sso) : await ConsumeSso(sso);
        if (userId == default)
            return AuthenticationError.NoAccountFound;

        // pixelrp: one login at a time per ACCOUNT, from the duplicate/sibling
        // checks (CanLogin) through registration. Two characters of one person
        // logging in at the same instant could each check-then-register before
        // the other registered, and both stay online - two wallets in a room.
        var gate = LoginGate(AccountUtility.RootOf(userId));
        await gate.WaitAsync();
        try
        {
            var canLogin = await CanLogin(userId);
            if (!canLogin)
                return AuthenticationError.LoginProhibited;

            // pixelrp: and never loaded while its last session is still on its
            // way out. CanLogin closes a session still open, but one already
            // closing on its own thread carries on there - and its save landed
            // AFTER this load read the database, so the new session started
            // from the balance before whatever the old one had just spent or
            // given away, and kept it. Waits for that save; refuses if it
            // never comes.
            if (!await PreviousSessionGone(userId))
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
                    _gameClientManager.UnregisterClient(habbo.Id, habbo.Username, session);
                    await MarkOffline(habbo.Id);
                    await OnHabboDisconnected(habbo);
                }
                return AuthenticationError.ConnectionClosed;
            }
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private const int TicketLifetimeSeconds = 180;

    // <hotel>-<unix time issued>-<uuid>, as User::ssoTicket writes it.
    private static readonly Regex IssuedTicket = new(@"-(\d{10})-[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);

    private static bool IsExpired(string sso)
    {
        var match = IssuedTicket.Match(sso);
        return match.Success && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - long.Parse(match.Groups[1].Value) > TicketLifetimeSeconds;
    }

    // pixelrp: one gate per account, so concurrent logins of an account's
    // characters are handled one after another. Bounded by the number of
    // accounts seen since start-up; each gate is a few bytes.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _loginGates = new();
    private static SemaphoreSlim LoginGate(int accountRoot) => _loginGates.GetOrAdd(accountRoot, _ => new SemaphoreSlim(1, 1));

    // pixelrp: clear the ticket and return its user in one transaction. The
    // row lock on SELECT ... FOR UPDATE means a second connection with the same
    // ticket blocks here, then reads NULL and gets nothing - the ticket is
    // single-use even under a dead heat.
    private async Task<int> ConsumeSso(string sso)
    {
        using var connection = _database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var userId = await connection.ExecuteScalarAsync<int>(
            "SELECT `id` FROM `users` WHERE `auth_ticket` = @sso FOR UPDATE", new { sso }, transaction);
        if (userId == default)
        {
            transaction.Rollback();
            return 0;
        }
        await connection.ExecuteAsync(
            "UPDATE `users` SET `auth_ticket` = NULL WHERE `id` = @userId", new { userId }, transaction);
        transaction.Commit();
        return userId;
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

    // pixelrp (Twist, 2026-10-27): 3 seconds, not 15. The whole login runs in
    // SSOTicketEvent, which PacketManager disconnects once it has taken 5
    // seconds - so a wait past that was cut off mid-way with no reason given,
    // while the login carried on alone in the background. Ending the wait
    // here leaves the rest of the login (loading the player, the first
    // messages) its last two seconds, and a refusal of its own, logged, in
    // place of a timeout. A ticket is used up either way; the website's
    // Reconnect fetches a new one.
    private const int PreviousSessionWaitMs = 3000;

    /// <summary>
    /// pixelrp: true once this character's previous session is gone - its
    /// logout saved (Habbo.LeavingTask) and it off the online list. A session
    /// still on the list with no logout begun by the deadline is a dead one,
    /// which RegisterClient displaces as it always has; a logout that never
    /// finishes saving refuses the login rather than load from under it.
    /// </summary>
    private async Task<bool> PreviousSessionGone(int userId)
    {
        var deadline = Environment.TickCount64 + PreviousSessionWaitMs;
        while (true)
        {
            var leaving = Habbo.LeavingTask(userId);
            if (!leaving.IsCompleted)
            {
                var left = deadline - Environment.TickCount64;
                if (left <= 0 || await Task.WhenAny(leaving, Task.Delay((int)left)) != leaving)
                    return false;
                continue;
            }
            if (_gameClientManager.GetClientByUserId(userId) == null || Environment.TickCount64 >= deadline)
                return true;
            await Task.Delay(50);
        }
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