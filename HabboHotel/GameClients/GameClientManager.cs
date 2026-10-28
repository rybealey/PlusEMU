using System.Collections.Concurrent;
using System.Data;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Notifications;
using Plus.Database;
using Plus.HabboHotel.Users.Messenger;

namespace Plus.HabboHotel.GameClients;

public class GameClientManager : IGameClientManager
{
    private readonly IDatabase _database;
    private readonly ILogger<GameClientManager> _logger;

    private readonly ConcurrentDictionary<int, GameClient> _userIdRegister;
    private readonly ConcurrentDictionary<string, GameClient> _usernameRegister;

    public GameClientManager(IDatabase database, ILogger<GameClientManager> logger)
    {
        _database = database;
        _logger = logger;
        _userIdRegister = new();
        _usernameRegister = new();
    }

    // pixelrp: the connection layer tracks sessions itself — online count and
    // hotel-wide client iteration must use the login register, same as
    // SendPacket.
    public int Count => _userIdRegister.Count;

    public ICollection<GameClient> GetClients => _userIdRegister.Values;

    public void OnCycle() { }

    // pixelrp: one lookup, not a check and a read - a player logging out
    // between the two threw KeyNotFoundException at whoever asked.
    public GameClient? GetClientByUserId(int userId) => _userIdRegister.TryGetValue(userId, out var client) ? client : null;

    public GameClient? GetClientByUsername(string username) => _usernameRegister.TryGetValue(username.ToLower(), out var client) ? client : null;

    public bool UpdateClientUsername(GameClient client, string oldUsername, string newUsername)
    {
        if (client == null || !_usernameRegister.ContainsKey(oldUsername.ToLower()))
            return false;
        _usernameRegister.TryRemove(oldUsername.ToLower(), out client);
        _usernameRegister.TryAdd(newUsername.ToLower(), client);
        return true;
    }

    public async Task<string> GetNameById(int id)
    {
        var client = GetClientByUserId(id);
        if (client != null)
            return client.GetHabbo().Username;
        using var connection = _database.Connection();
        return await connection.QuerySingleOrDefaultAsync<string>("SELECT username FROM users WHERE id = @id LIMIT 1", new { id });
    }

    public IEnumerable<GameClient> GetClientsById(Dictionary<int, MessengerBuddy>.KeyCollection users)
    {
        foreach (var id in users)
        {
            var client = GetClientByUserId(id);
            if (client != null)
                yield return client;
        }
    }

    public void ModAlert(string message)
    {
        foreach (var client in GetClients.ToList())
        {
            if (client == null || client.GetHabbo() == null)
                continue;
            if (client.GetHabbo().Permissions.HasRight("mod_tool") && !client.GetHabbo().Permissions.HasRight("staff_ignore_mod_alert"))
            {
                try
                {
                    //client.SendWhisper(message, 5);
                }
                catch { }
            }
        }
    }

    public void DoAdvertisingReport(GameClient reporter, GameClient target)
    {
        if (reporter == null || target == null || reporter.GetHabbo() == null || target.GetHabbo() == null)
            return;
        var builder = new StringBuilder();
        builder.Append("New report submitted!\r\r");
        builder.Append($"Reporter: {reporter.GetHabbo().Username}\r");
        builder.Append($"Reported User: {target.GetHabbo().Username}\r\r");
        builder.Append($"{target.GetHabbo().Username}s last 10 messages:\r\r");
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery($"SELECT `message` FROM `chatlogs` WHERE `user_id` = '{target.GetHabbo().Id}' ORDER BY `id` DESC LIMIT 10");
            var logs = dbClient.GetTable();
            if (logs != null)
            {
                var number = 11;
                foreach (DataRow log in logs.Rows)
                {
                    number -= 1;
                    builder.Append($"{number}: {Convert.ToString(log["message"])}\r");
                }
            }
        }
        foreach (var client in GetClients.ToList())
        {
            if (client == null || client.GetHabbo() == null)
                continue;
            if (client.GetHabbo().Permissions.HasRight("mod_tool") && !client.GetHabbo().Permissions.HasRight("staff_ignore_advertisement_reports"))
                client.Send(new MotdNotificationComposer(builder.ToString()));
        }
    }


    public void SendPacket(IServerPacket packet, string fuse = "")
    {
        // pixelrp: the connection layer tracks sessions itself — hotel-wide
        // sends must use the login register.
        // pixelrp (Twist, 2026-10-28): composed once (SharedFrame), as a room
        // broadcast is - it was composed again for every player online.
        var frame = new SharedFrame(packet);
        foreach (var client in _userIdRegister.Values.ToList())
        {
            if (client == null || client.GetHabbo() == null)
                continue;
            if (!string.IsNullOrEmpty(fuse))
            {
                if (!client.GetHabbo().Permissions.HasRight(fuse))
                    continue;
            }
            frame.SendTo(client);
        }
    }

    public void LogClonesOut(int userId)
    {
        var client = GetClientByUserId(userId);
        if (client != null)
            client.Disconnect();
    }

    public void RegisterClient(GameClient client, int userId, string username)
    {
        // pixelrp: a second login for the same player replaces the first and
        // the first is disconnected, so two live sessions can never share one
        // id (each would carry its own copy of the wallet). The displaced
        // session's own logout runs the conditional UnregisterClient below,
        // which then leaves this new entry alone.
        if (_userIdRegister.TryGetValue(userId, out var displaced) && displaced != client)
            displaced.Disconnect();
        _userIdRegister[userId] = client;
        _usernameRegister[username.ToLower()] = client;
    }

    public void UnregisterClient(int userid, string username) => UnregisterClient(userid, username, null);

    // pixelrp: with a client, the entry is cleared only if it is still THIS
    // client's - a ghost session logging out after a reconnect must not evict
    // the live session that replaced it. Without one (the old call), it clears
    // unconditionally, as before.
    public void UnregisterClient(int userid, string username, GameClient? client)
    {
        if (client == null)
        {
            _userIdRegister.TryRemove(userid, out _);
            _usernameRegister.TryRemove(username.ToLower(), out _);
            return;
        }
        _userIdRegister.TryRemove(new KeyValuePair<int, GameClient>(userid, client));
        _usernameRegister.TryRemove(new KeyValuePair<string, GameClient>(username.ToLower(), client));
    }

    public void CloseAll()
    {
        foreach (var client in GetClients.ToList())
        {
            if (client.GetHabbo() != null)
            {
                try
                {
                    using (var dbClient = _database.GetQueryReactor())
                    {
                        dbClient.RunQuery(client.GetHabbo().GetQueryString);
                    }
                    Console.Clear();
                    _logger.LogInformation("<<- SERVER SHUTDOWN ->> IVNENTORY IS SAVING");
                }
                catch { }
            }
        }
        _logger.LogInformation("Done saving users inventory!");
        _logger.LogInformation("Closing server connections...");
        _userIdRegister.Clear();
        _usernameRegister.Clear();
        _logger.LogInformation("Connections closed!");
    }
}