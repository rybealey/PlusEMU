using System.Collections.Concurrent;
using System.Text;
using Plus.Communication.Packets.Outgoing.Notifications;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired;

namespace Plus.HabboHotel.Rooms.Chat.Commands;

public class CommandManager : ICommandManager
{
    private readonly IGameClientManager _gameClientManager;
    private readonly IDatabase _database;
    /// <summary>
    /// Commands registered for use.
    /// </summary>
    private readonly ConcurrentDictionary<string, ICommandBase> _commands;
    /// <summary>
    /// Command Prefix only applies to custom commands.
    /// </summary>
    private readonly string _prefix = ":";

    /// <summary>
    /// The player commands a knocked-out player (0 health) CAN still use. Every
    /// other command under Commands.User is refused (see KnockedOut); staff
    /// commands, under Moderator and Administrator, are never refused.
    ///
    /// An allow list rather than a block list, so a new command is refused
    /// while out cold until somebody decides otherwise. What is here only
    /// looks something up, sets a preference, or talks - :ga and :ca are gang
    /// and corporation chat, and talking stays open. :911 / :999 too: someone
    /// lying knocked out is who most needs to call it.
    /// </summary>
    private static readonly HashSet<string> KnockedOutCan = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "stats",
        "dnd", "disablegifts", "disablemimic", "flagme",
        "ga", "ca",
        "911", "999"
    };

    private static bool IsPlayerCommand(ICommandBase command) =>
        (command.GetType().Namespace ?? string.Empty).Contains(".Commands.User", StringComparison.Ordinal);

    /// <summary>
    /// The default initializer for the CommandManager
    /// </summary>
    public CommandManager(IEnumerable<ICommandBase> commands, IGameClientManager gameClientManager, IDatabase database)
    {
        _gameClientManager = gameClientManager;
        _database = database;
        _commands = new(commands.ToDictionary(command => command.Key));
    }

    /// <summary>
    /// Request the text to parse and check for commands that need to be executed.
    /// </summary>
    /// <param name="session">Session calling this method.</param>
    /// <param name="message">The message to parse.</param>
    /// <returns>True if parsed or false if not.</returns>
    public async Task<bool> Parse(GameClient session, string message)
    {
        if (session == null || session.GetHabbo() == null || session.GetHabbo().CurrentRoom == null)
            return false;
        if (!message.StartsWith(_prefix))
            return false;
        if (message == $"{_prefix}commands")
        {
            // Group the caller's available commands by tier, derived from each
            // command's namespace (.../Commands/<Tier>/Foo.cs), and emit them as
            // readable text with [Tier] section markers. The client parses this
            // into a grouped, filterable table; the marker line keeps it distinct
            // from every other MOTD so nothing else is affected.
            var tierOrder = new[] { "User", "Moderator", "Administrator" };
            var byTier = new Dictionary<string, List<ICommandBase>>();
            foreach (var cmdList in _commands.ToList())
            {
                var cmd = cmdList.Value;
                if (!string.IsNullOrEmpty(cmd.PermissionRequired) && !session.GetHabbo().Permissions.HasCommand(cmd.PermissionRequired))
                    continue;
                var ns = cmd.GetType().Namespace ?? string.Empty;
                var tier = ns.Substring(ns.LastIndexOf('.') + 1);
                if (Array.IndexOf(tierOrder, tier) < 0)
                    tier = "Other";
                if (!byTier.TryGetValue(tier, out var bucket))
                    byTier[tier] = bucket = new List<ICommandBase>();
                bucket.Add(cmd);
            }
            var list = new StringBuilder();
            list.Append("This is the list of commands you have available:\n");
            foreach (var tier in tierOrder.Append("Other"))
            {
                if (!byTier.TryGetValue(tier, out var bucket) || bucket.Count == 0)
                    continue;
                list.Append($"[{tier}]\n");
                foreach (var cmd in bucket.OrderBy(c => c.Key))
                    list.Append($":{cmd.Key} {cmd.Parameters} - {cmd.Description}\n");
            }
            session.Send(new MotdNotificationComposer(list.ToString()));
            return true;
        }
        message = message.Substring(1);
        if (string.IsNullOrWhiteSpace(message))
            return false;

        // pixelrp: empty pieces dropped, so a stray or doubled space never
        // reads as a blank player name ("User  seems to be offline").
        var split = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0)
            return false;
        var key = split[0];
        var parameters = split.Length > 1 ? split[1..] : Array.Empty<string>();
        if (_commands.TryGetValue(key.ToLower(), out var command))
        {
            if (session.GetHabbo().Permissions.HasRight("mod_tool"))
                LogCommand(session.GetHabbo().Id, message, session.GetHabbo().MachineId);
            if (!string.IsNullOrEmpty(command.PermissionRequired))
            {
                if (!session.GetHabbo().Permissions.HasCommand(command.PermissionRequired))
                {
                    // pixelrp: :sa from a non-staff player - the chat box kept
                    // the prefix on send (RpRetainChatPrefixComposer), so tell it
                    // this one did not go out.
                    if (RpRetainChatPrefixComposer.Retains(key.ToLower()))
                        session.Send(RpRetainChatPrefixComposer.Drop());
                    return false;
                }
            }
            // pixelrp police: cuffs stop the things you do with your hands.
            // Here rather than inside each command, because eleven copies of
            // the same check is eleven places for the next verb to be
            // forgotten - and the forgetting would look like a feature.
            if (User.Police.PoliceState.Blocks(session.GetHabbo().Id, key.ToLower()))
            {
                session.SendWhisper("Your hands are cuffed.");
                return true;
            }
            // pixelrp police: pepper spray - nobody fights while they stumble.
            if (User.Police.PoliceState.DisorientedBlocks(session.GetHabbo().Id, key.ToLower()))
            {
                session.SendWhisper("You are too disoriented to do that.");
                return true;
            }
            // pixelrp jail: a prisoner walks and talks, and that is all.
            if (User.Police.JailState.Blocks(session.GetHabbo().Id, key.ToLower()))
            {
                session.SendWhisper("You can't do that in jail.");
                return true;
            }
            // pixelrp City Panel: staff can pause fighting city-wide. Backpack
            // weapon clicks come through here as commands too.
            if (Plus.HabboHotel.CityPanel.CityWorld.CombatBlocks(key.ToLower()))
            {
                session.SendWhisper("Fighting is paused city-wide.");
                return true;
            }
            // pixelrp: out cold means doing nothing. Same reasoning as the cuffs:
            // one check here, not a copy in every command.
            if (IsPlayerCommand(command) && !KnockedOutCan.Contains(key) && KnockedOut.Refuse(session))
                return true;
            session.GetHabbo().ChatCommand = command;
            session.GetHabbo().CurrentRoom.GetWired().TriggerEvent(WiredBoxType.TriggerUserSaysCommand, session.GetHabbo(), this);

            if (command is IChatCommand chatCommand)
            {
                chatCommand.Execute(session, session.GetHabbo().CurrentRoom, parameters);
            }
            else if (command is ITargetChatCommand targetChatCommand)
            {
                // pixelrp: who it is aimed at. A name typed first wins. Failing
                // that, the player's HUD target (Habbo.RpHudTargetId) - so
                // ":kiss" alone kisses whoever is selected, and ":charge theft"
                // charges them, every word kept for the command. "x" is the
                // client's shorthand for the HUD target, swapped for the name
                // before sending; one that arrives as a bare "x" means the
                // client had nobody selected, and it is dropped the same way.
                var typed = parameters.Length > 0 ? parameters[0] : null;
                var typedX = typed != null && typed.Equals("x", StringComparison.OrdinalIgnoreCase);
                GameClient? target = null;
                if (typed != null && !typedX)
                {
                    target = _gameClientManager.GetClientByUsername(typed);
                    if (target != null)
                        parameters = parameters[1..];
                }
                else if (typedX)
                {
                    parameters = parameters[1..];
                }

                if (target?.GetHabbo() == null)
                {
                    var hudTargetId = session.GetHabbo().RpHudTargetId;
                    target = hudTargetId > 0 ? _gameClientManager.GetClientByUserId(hudTargetId) : null;
                }

                if (target?.GetHabbo() == null)
                {
                    // Nothing typed (or only "x") and nobody selected; or a name
                    // that is not online, with nobody selected to fall back on.
                    session.SendWhisper(typed == null || typedX ? targetChatCommand.NoTargetMessage : $"User {typed} seems to be offline.");
                    return true;
                }

                var targetHabbo = target.GetHabbo();
                if (targetChatCommand.MustBeInSameRoom && session.GetHabbo().CurrentRoom != targetHabbo.CurrentRoom)
                {
                    session.SendWhisper(targetChatCommand.IsRanged ? RangeMessages.NotInRoom : $"You must be in the same room as {targetHabbo.Username} to execute this command.");
                    return true;
                }

                // A command missing the rest of what it needs (":ban" with no
                // length, ":givebadge" with no badge) throws reading it, and a
                // packet that throws disconnects the player. The HUD target
                // makes those easy to send, so answer with the usage instead.
                try
                {
                    await targetChatCommand.Execute(session, session.GetHabbo().CurrentRoom, targetHabbo, parameters);
                }
                catch (Exception e) when (e is IndexOutOfRangeException or FormatException or OverflowException or ArgumentOutOfRangeException)
                {
                    session.SendWhisper($"Usage: :{key.ToLower()} {targetChatCommand.Parameters.Trim()}");
                }
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Registers a Chat Command.
    ///
    /// NO IN-TREE CALLERS, AND IT STILL HAS TO EXIST. Every command in this
    /// build arrives through the constructor instead - Program.cs scans the
    /// assembly for ICommandBase and the ctor keys them by command.Key - so
    /// grepping for callers of this finds nothing and it reads as dead code.
    /// It was deleted on that basis on 2026-09-21 and broke the build: it is
    /// declared on ICommandManager, and an interface member has no callers by
    /// definition. Plugins are loaded as separate assemblies (Program.cs
    /// AddPlugin), so a caller need not be in this repo at all.
    /// </summary>
    /// <param name="commandText">Text to type for this command.</param>
    /// <param name="command">The command to execute.</param>
    public void Register(string commandText, ICommandBase command)
    {
        _commands.TryAdd(commandText, command);
    }

    public static string MergeParams(string[] @params, int start = 0)
    {
        var merged = new StringBuilder();
        for (var i = start; i < @params.Length; i++)
        {
            if (i > start)
                merged.Append(" ");
            merged.Append(@params[i]);
        }
        return merged.ToString();
    }

    public void LogCommand(int userId, string data, string machineId)
    {
        using var dbClient = _database.GetQueryReactor();
        dbClient.SetQuery("INSERT INTO `logs_client_staff` (`user_id`,`data_string`,`machine_id`, `timestamp`) VALUES (@UserId,@Data,@MachineId,@Timestamp)");
        dbClient.AddParameter("UserId", userId);
        dbClient.AddParameter("Data", data);
        dbClient.AddParameter("MachineId", machineId ?? string.Empty);
        dbClient.AddParameter("Timestamp", PlusEnvironment.GetUnixTimestamp());
        dbClient.RunQuery();
    }

    public bool TryGetCommand(string command, out ICommandBase chatCommand) => _commands.TryGetValue(command, out chatCommand);
}