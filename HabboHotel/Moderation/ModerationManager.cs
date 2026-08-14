using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plus.Database;
using Plus.HabboHotel.Users;
using Plus.Utilities;

namespace Plus.HabboHotel.Moderation;

public sealed class ModerationManager : IModerationManager
{
    private readonly IDatabase _database;
    private readonly ILogger<ModerationManager> _logger;
    private readonly Dictionary<string, ModerationBan> _bans = new();
    private readonly Dictionary<int, List<ModerationPresetActions>> _moderationCfhTopicActions = new();


    private readonly Dictionary<int, string> _moderationCfhTopics = new();
    private readonly ConcurrentDictionary<int, ModerationTicket> _modTickets = new();
    private readonly List<string> _roomPresets = new();
    private readonly Dictionary<int, string> _userActionPresetCategories = new();
    private readonly Dictionary<int, List<ModerationPresetActionMessages>> _userActionPresetMessages = new();
    private readonly List<string> _userPresets = new();

    /// <summary>
    /// Ids for tickets that could not be written (see <see cref="InsertTicket" />).
    /// Counts down from 0 so these can never collide with a real row id.
    /// </summary>
    private int _unpersistedTicketId;

    public ICollection<string> UserMessagePresets => _userPresets;

    public ICollection<string> RoomMessagePresets => _roomPresets;

    public ICollection<ModerationTicket> GetTickets => _modTickets.Values;

    public ModerationManager(IDatabase database, ILogger<ModerationManager> logger)
    {
        _database = database;
        _logger = logger;
    }

    public Dictionary<string, List<ModerationPresetActions>> UserActionPresets
    {
        get
        {
            var result = new Dictionary<string, List<ModerationPresetActions>>();
            foreach (var category in _moderationCfhTopics.ToList())
            {
                result.Add(category.Value, new());
                if (_moderationCfhTopicActions.ContainsKey(category.Key))
                    foreach (var data in _moderationCfhTopicActions[category.Key])
                        result[category.Value].Add(data);
            }
            return result;
        }
    }

    public void Init()
    {
        if (_userPresets.Count > 0)
            _userPresets.Clear();
        if (_moderationCfhTopics.Count > 0)
            _moderationCfhTopics.Clear();
        if (_moderationCfhTopicActions.Count > 0)
            _moderationCfhTopicActions.Clear();
        if (_bans.Count > 0)
            _bans.Clear();
        using (var dbClient = _database.GetQueryReactor())
        {
            DataTable presetsTable = null;
            dbClient.SetQuery("SELECT * FROM `moderation_presets`;");
            presetsTable = dbClient.GetTable();
            if (presetsTable != null)
            {
                foreach (DataRow row in presetsTable.Rows)
                {
                    var type = Convert.ToString(row["type"]).ToLower();
                    switch (type)
                    {
                        case "user":
                            _userPresets.Add(Convert.ToString(row["message"]));
                            break;
                        case "room":
                            _roomPresets.Add(Convert.ToString(row["message"]));
                            break;
                    }
                }
            }
        }
        using (var dbClient = _database.GetQueryReactor())
        {
            DataTable moderationTopics = null;
            dbClient.SetQuery("SELECT * FROM `moderation_topics`;");
            moderationTopics = dbClient.GetTable();
            if (moderationTopics != null)
            {
                foreach (DataRow row in moderationTopics.Rows)
                {
                    if (!_moderationCfhTopics.ContainsKey(Convert.ToInt32(row["id"])))
                        _moderationCfhTopics.Add(Convert.ToInt32(row["id"]), Convert.ToString(row["caption"]));
                }
            }
        }
        using (var dbClient = _database.GetQueryReactor())
        {
            DataTable moderationTopicsActions = null;
            dbClient.SetQuery("SELECT * FROM `moderation_topic_actions`;");
            moderationTopicsActions = dbClient.GetTable();
            if (moderationTopicsActions != null)
            {
                foreach (DataRow row in moderationTopicsActions.Rows)
                {
                    var parentId = Convert.ToInt32(row["parent_id"]);
                    if (!_moderationCfhTopicActions.ContainsKey(parentId)) _moderationCfhTopicActions.Add(parentId, new());
                    _moderationCfhTopicActions[parentId].Add(new(Convert.ToInt32(row["id"]), Convert.ToInt32(row["parent_id"]), Convert.ToString(row["type"]),
                        Convert.ToString(row["caption"]), Convert.ToString(row["message_text"]),
                        Convert.ToInt32(row["mute_time"]), Convert.ToInt32(row["ban_time"]), Convert.ToInt32(row["ip_time"]), Convert.ToInt32(row["trade_lock_time"]),
                        Convert.ToString(row["default_sanction"])));
                }
            }
        }
        using (var dbClient = _database.GetQueryReactor())
        {
            DataTable presetsActionCats = null;
            dbClient.SetQuery("SELECT * FROM `moderation_preset_action_categories`;");
            presetsActionCats = dbClient.GetTable();
            if (presetsActionCats != null)
                foreach (DataRow row in presetsActionCats.Rows)
                    _userActionPresetCategories.Add(Convert.ToInt32(row["id"]), Convert.ToString(row["caption"]));
        }
        using (var dbClient = _database.GetQueryReactor())
        {
            DataTable presetsActionMessages = null;
            dbClient.SetQuery("SELECT * FROM `moderation_preset_action_messages`;");
            presetsActionMessages = dbClient.GetTable();
            if (presetsActionMessages != null)
            {
                foreach (DataRow row in presetsActionMessages.Rows)
                {
                    var parentId = Convert.ToInt32(row["parent_id"]);
                    if (!_userActionPresetMessages.ContainsKey(parentId)) _userActionPresetMessages.Add(parentId, new());
                    _userActionPresetMessages[parentId].Add(new(Convert.ToInt32(row["id"]), Convert.ToInt32(row["parent_id"]), Convert.ToString(row["caption"]),
                        Convert.ToString(row["message_text"]),
                        Convert.ToInt32(row["mute_hours"]), Convert.ToInt32(row["ban_hours"]), Convert.ToInt32(row["ip_ban_hours"]), Convert.ToInt32(row["trade_lock_days"]),
                        Convert.ToString(row["notice"])));
                }
            }
        }
        using (var dbClient = _database.GetQueryReactor())
        {
            DataTable getBans = null;
            dbClient.SetQuery("SELECT `bantype`,`value`,`reason`,`expire` FROM `bans` WHERE `bantype` = 'machine' OR `bantype` = 'user'");
            getBans = dbClient.GetTable();
            if (getBans != null)
            {
                foreach (DataRow dRow in getBans.Rows)
                {
                    var value = Convert.ToString(dRow["value"]);
                    var reason = Convert.ToString(dRow["reason"]);
                    var expires = (double)dRow["expire"];
                    var type = Convert.ToString(dRow["bantype"]);
                    var ban = new ModerationBan(BanTypeUtility.GetModerationBanType(type), value, reason, expires);
                    if (ban != null)
                    {
                        if (expires > PlusEnvironment.GetUnixTimestamp())
                        {
                            if (!_bans.ContainsKey(value))
                                _bans.Add(value, ban);
                        }
                        else
                        {
                            dbClient.SetQuery($"DELETE FROM `bans` WHERE `bantype` = '{BanTypeUtility.FromModerationBanType(ban.Type)}' AND `value` = @Key LIMIT 1");
                            dbClient.AddParameter("Key", value);
                            dbClient.RunQuery();
                        }
                    }
                }
            }
        }
        _logger.LogInformation("Loaded " + (_userPresets.Count + _roomPresets.Count) + " moderation presets.");
        _logger.LogInformation("Loaded " + _userActionPresetCategories.Count + " moderation categories.");
        _logger.LogInformation("Loaded " + _userActionPresetMessages.Count + " moderation action preset messages.");
        _logger.LogInformation("Cached " + _bans.Count + " username and machine bans.");
    }

    /// <summary>
    /// Loads open/picked tickets from `moderation_tickets` into the live in-memory
    /// queue. Boot-only — deliberately not part of <see cref="Init" />, which also
    /// runs from the in-game ":update moderation" command to reload presets. Ticket
    /// state is live (fallback negative ids for unpersisted tickets, in-session
    /// closes that a DB write may not have caught up with yet); a preset reload must
    /// not clear-and-reload it out from under staff. See also: the ids that
    /// <see cref="InsertTicket" /> hands out when a write fails are stored only in
    /// memory and would be discarded permanently by a reload.
    /// </summary>
    public void LoadTickets()
    {
        using var dbClient = _database.GetQueryReactor();
        // LEFT JOINs rather than INNER: a row whose users have since been
        // deleted must be counted and reported, not silently dropped by the
        // query.
        dbClient.SetQuery(
            "SELECT `t`.`id`, `t`.`score`, `t`.`type`, `t`.`category`, `t`.`message`, `t`.`reported_chats`, `t`.`room_id`, `t`.`room_name`, `t`.`timestamp`, " +
            "`t`.`sender_id`, `s`.`username` AS `sender_username`, `t`.`reported_id`, `r`.`username` AS `reported_username`, " +
            "`t`.`moderator_id`, `m`.`username` AS `moderator_username` " +
            "FROM `moderation_tickets` AS `t` " +
            "LEFT JOIN `users` AS `s` ON `s`.`id` = `t`.`sender_id` " +
            "LEFT JOIN `users` AS `r` ON `r`.`id` = `t`.`reported_id` " +
            "LEFT JOIN `users` AS `m` ON `m`.`id` = `t`.`moderator_id` " +
            "WHERE `t`.`status` IN ('open', 'picked') ORDER BY `t`.`id`;");
        var openTickets = dbClient.GetTable();
        var skippedIds = new List<int>();
        if (openTickets != null)
        {
            foreach (DataRow row in openTickets.Rows)
            {
                var ticketId = Convert.ToInt32(row["id"]);
                var senderUsername = row["sender_username"] == DBNull.Value ? string.Empty : Convert.ToString(row["sender_username"]);
                var reportedUsername = row["reported_username"] == DBNull.Value ? string.Empty : Convert.ToString(row["reported_username"]);

                // A ticket that can no longer name who reported whom is no use
                // to staff.
                if (string.IsNullOrEmpty(senderUsername) || string.IsNullOrEmpty(reportedUsername))
                {
                    skippedIds.Add(ticketId);
                    continue;
                }

                // A single unreadable row (e.g. a value this migrated database
                // cannot be trusted to hold cleanly) must cost one ticket, not
                // every boot.
                try
                {
                    var timestamp = Convert.ToDouble(row["timestamp"]);

                    // A timestamp of 0, one stored in milliseconds instead of seconds,
                    // or anything else outside a sane range would throw out of
                    // FromUnixTimestamp/AgeInMilliseconds later — inside a packet
                    // Compose(), which disconnects whoever is being sent it. Catch it
                    // here instead, where the cost is one skipped row.
                    if (!UnixTimestamp.IsValid(timestamp))
                    {
                        _logger.LogWarning("Moderation ticket {Id} has an invalid timestamp ({Timestamp}); skipping it.", ticketId, timestamp);
                        continue;
                    }

                    var moderatorUsername = row["moderator_username"] == DBNull.Value ? string.Empty : Convert.ToString(row["moderator_username"]);

                    // The moderator who picked it has since been deleted; hand the
                    // ticket back to the open queue rather than to a blank name.
                    var moderatorId = string.IsNullOrEmpty(moderatorUsername) ? 0 : Convert.ToInt32(row["moderator_id"]);
                    var ticket = new ModerationTicket(ticketId, Convert.ToInt32(row["type"]), Convert.ToInt32(row["category"]),
                        timestamp, Convert.ToInt32(row["score"]),
                        Convert.ToInt32(row["sender_id"]), senderUsername,
                        Convert.ToInt32(row["reported_id"]), reportedUsername,
                        moderatorId, moderatorUsername,
                        Convert.ToString(row["message"]), Convert.ToUInt32(row["room_id"]), Convert.ToString(row["room_name"]),
                        ParseReportedChats(ticketId, row["reported_chats"]));
                    _modTickets.TryAdd(ticket.Id, ticket);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not load moderation ticket {Id}; skipping it.", ticketId);
                }
            }
        }
        if (skippedIds.Count > 0)
            _logger.LogWarning("Skipped {Count} moderation tickets whose reporter or reported user no longer exists: {Ids}",
                skippedIds.Count, string.Join(", ", skippedIds));
        _logger.LogInformation("Loaded " + _modTickets.Count + " open moderation tickets.");
    }

    public void ReCacheBans()
    {
        if (_bans.Count > 0)
            _bans.Clear();
        using (var dbClient = _database.GetQueryReactor())
        {
            DataTable getBans = null;
            dbClient.SetQuery("SELECT `bantype`,`value`,`reason`,`expire` FROM `bans` WHERE `bantype` = 'machine' OR `bantype` = 'user'");
            getBans = dbClient.GetTable();
            if (getBans != null)
            {
                foreach (DataRow dRow in getBans.Rows)
                {
                    var value = Convert.ToString(dRow["value"]);
                    var reason = Convert.ToString(dRow["reason"]);
                    var expires = (double)dRow["expire"];
                    var type = Convert.ToString(dRow["bantype"]);
                    var ban = new ModerationBan(BanTypeUtility.GetModerationBanType(type), value, reason, expires);
                    if (ban != null)
                    {
                        if (expires > PlusEnvironment.GetUnixTimestamp())
                        {
                            if (!_bans.ContainsKey(value))
                                _bans.Add(value, ban);
                        }
                        else
                        {
                            dbClient.SetQuery($"DELETE FROM `bans` WHERE `bantype` = '{BanTypeUtility.FromModerationBanType(ban.Type)}' AND `value` = @Key LIMIT 1");
                            dbClient.AddParameter("Key", value);
                            dbClient.RunQuery();
                        }
                    }
                }
            }
        }
        _logger.LogInformation("Cached " + _bans.Count + " username and machine bans.");
    }

    public void BanUser(string mod, ModerationBanType type, string banValue, string reason, double expireTimestamp)
    {
        var banType = type == ModerationBanType.Ip ? "ip" : type == ModerationBanType.Machine ? "machine" : "user";
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery(
                $"REPLACE INTO `bans` (`bantype`, `value`, `reason`, `expire`, `added_by`,`added_date`) VALUES ('{banType}', '{banValue}', @reason, {expireTimestamp}, '{mod}', '{PlusEnvironment.GetUnixTimestamp()}');");
            dbClient.AddParameter("reason", reason);
            dbClient.RunQuery();
        }
        if (type == ModerationBanType.Machine || type == ModerationBanType.Username)
        {
            if (!_bans.ContainsKey(banValue))
                _bans.Add(banValue, new(type, banValue, reason, expireTimestamp));
        }
    }

    public bool TryAddTicket(ModerationTicket ticket)
    {
        ticket.Id = InsertTicket(ticket);
        return _modTickets.TryAdd(ticket.Id, ticket);
    }

    public void PickTicket(ModerationTicket ticket, Habbo moderator)
    {
        // Already closed: picking it would resurrect a resolved/withdrawn ticket as
        // a live "picked" one, and it would load that way after the next restart.
        if (ticket.Answered)
            return;
        ticket.ModeratorId = moderator.Id;
        ticket.ModeratorUsername = moderator.Username;
        UpdateTicketStatus(ticket, ModerationTicketStatus.Picked);
    }

    public void ReleaseTicket(ModerationTicket ticket)
    {
        // Already closed: releasing it would destroy the recorded outcome and who
        // handled it, reopening a ticket that already has a final status.
        if (ticket.Answered)
            return;
        ticket.ModeratorId = 0;
        ticket.ModeratorUsername = string.Empty;
        UpdateTicketStatus(ticket, ModerationTicketStatus.Open);
    }

    public void CloseTicket(ModerationTicket ticket, ModerationTicketStatus status)
    {
        ticket.Answered = true;
        UpdateTicketStatus(ticket, status);
    }

    /// <summary>
    /// Writes a new report and returns its row id. A database failure must not
    /// cost the hotel the report — the query adapter logs and returns 0, so fall
    /// back to a negative id that keeps the ticket usable in memory for this
    /// session and is plainly not a row.
    /// </summary>
    private int InsertTicket(ModerationTicket ticket)
    {
        using var dbClient = _database.GetQueryReactor();
        if (dbClient == null)
        {
            _logger.LogError("Could not save the moderation ticket from user {SenderId}: no database connection was available. It will reach staff this session but is lost on the next restart.",
                ticket.SenderId);
            return Interlocked.Decrement(ref _unpersistedTicketId);
        }
        dbClient.SetQuery(
            "INSERT INTO `moderation_tickets` (`score`,`type`,`category`,`status`,`sender_id`,`reported_id`,`moderator_id`,`message`,`reported_chats`,`room_id`,`room_name`,`timestamp`) " +
            "VALUES (@score, @type, @category, 'open', @senderId, @reportedId, 0, @message, @reportedChats, @roomId, @roomName, @timestamp);");
        dbClient.AddParameter("score", ticket.Priority);
        dbClient.AddParameter("type", ticket.Type);
        dbClient.AddParameter("category", ticket.Category);
        dbClient.AddParameter("senderId", ticket.SenderId);
        dbClient.AddParameter("reportedId", ticket.ReportedId);
        dbClient.AddParameter("message", ticket.Issue);
        dbClient.AddParameter("reportedChats", JsonSerializer.Serialize(ticket.ReportedChats));
        dbClient.AddParameter("roomId", ticket.RoomId);
        dbClient.AddParameter("roomName", ticket.RoomName);
        dbClient.AddParameter("timestamp", ticket.Timestamp);
        var id = Convert.ToInt32(dbClient.InsertQuery());
        if (id > 0)
            return id;
        _logger.LogError("Could not save the moderation ticket from user {SenderId}. It will reach staff this session but is lost on the next restart.",
            ticket.SenderId);
        return Interlocked.Decrement(ref _unpersistedTicketId);
    }

    /// <summary>Writes a ticket's status and picking moderator through to its row.</summary>
    private void UpdateTicketStatus(ModerationTicket ticket, ModerationTicketStatus status)
    {
        if (ticket.Id <= 0) // Never persisted (see InsertTicket) — no row to update.
            return;
        using var dbClient = _database.GetQueryReactor();
        if (dbClient == null)
        {
            _logger.LogError("Could not update moderation ticket {Id}: no database connection was available.", ticket.Id);
            return;
        }
        dbClient.SetQuery("UPDATE `moderation_tickets` SET `status` = @status, `moderator_id` = @moderatorId WHERE `id` = @id LIMIT 1;");
        dbClient.AddParameter("status", status.ToString().ToLowerInvariant());
        dbClient.AddParameter("moderatorId", ticket.ModeratorId);
        dbClient.AddParameter("id", ticket.Id);
        dbClient.RunQuery();
    }

    /// <summary>
    /// The quoted chat lines are stored as a JSON array. Anything unreadable loads
    /// as no chats rather than taking startup down with it.
    /// </summary>
    private List<string> ParseReportedChats(int ticketId, object value)
    {
        if (value == DBNull.Value)
            return new();
        var raw = Convert.ToString(value);
        if (string.IsNullOrEmpty(raw))
            return new();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw) ?? new();
        }
        catch (JsonException)
        {
            _logger.LogWarning("Could not read the quoted chats on moderation ticket {Id}; loading it without them.", ticketId);
            return new();
        }
    }

    public bool TryGetTicket(int ticketId, out ModerationTicket ticket) => _modTickets.TryGetValue(ticketId, out ticket);

    public bool UserHasTickets(int userId) => _modTickets.Any(x => x.Value.SenderId == userId && x.Value.Answered == false);

    public ModerationTicket GetTicketBySenderId(int userId) =>
        _modTickets.FirstOrDefault(x => x.Value.SenderId == userId && x.Value.Answered == false).Value;

    /// <summary>
    /// Runs a quick check to see if a ban record is cached in the server.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="ban"></param>
    /// <returns></returns>
    public bool IsBanned(string key, out ModerationBan ban)
    {
        if (_bans.TryGetValue(key, out ban))
        {
            if (!ban.Expired)
                return true;

            //This ban has expired, let us quickly remove it here.
            using (var dbClient = _database.GetQueryReactor())
            {
                dbClient.SetQuery($"DELETE FROM `bans` WHERE `bantype` = '{BanTypeUtility.FromModerationBanType(ban.Type)}' AND `value` = @Key LIMIT 1");
                dbClient.AddParameter("Key", key);
                dbClient.RunQuery();
            }

            //And finally, let us remove the ban record from the cache.
            _bans.Remove(key);
            return false;
        }
        return false;
    }

    /// <summary>
    /// Run a quick database check to see if this ban exists in the database.
    /// </summary>
    /// <param name="machineId">The value of the ban.</param>
    /// <returns></returns>
    public bool HasMachineBanCheck(string machineId)
    {
        ModerationBan machineBanRecord = null;
        if (IsBanned(machineId, out machineBanRecord))
        {
            DataRow banRow = null;
            using var dbClient = _database.GetQueryReactor();
            dbClient.SetQuery("SELECT * FROM `bans` WHERE `bantype` = 'machine' AND `value` = @value LIMIT 1");
            dbClient.AddParameter("value", machineId);
            banRow = dbClient.GetRow();

            //If there is no more ban record, then we can simply remove it from our cache!
            if (banRow == null)
            {
                RemoveBan(machineId);
                return false;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Run a quick database check to see if this ban exists in the database.
    /// </summary>
    /// <param name="username">The value of the ban.</param>
    /// <returns></returns>
    public bool UsernameBanCheck(string username)
    {
        ModerationBan usernameBanRecord = null;
        if (IsBanned(username, out usernameBanRecord))
        {
            DataRow banRow = null;
            using var dbClient = _database.GetQueryReactor();
            dbClient.SetQuery("SELECT * FROM `bans` WHERE `bantype` = 'user' AND `value` = @value LIMIT 1");
            dbClient.AddParameter("value", username);
            banRow = dbClient.GetRow();

            //If there is no more ban record, then we can simply remove it from our cache!
            if (banRow == null)
            {
                RemoveBan(username);
                return false;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Remove a ban from the cache based on a given value.
    /// </summary>
    /// <param name="value"></param>
    public void RemoveBan(string value)
    {
        _bans.Remove(value);
    }
}