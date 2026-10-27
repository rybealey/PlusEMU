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
    /// looks something up, sets a preference, or talks - :ga, :ca and :pa are
    /// gang, corporation and partner chat, and talking stays open. :911 / :999 are NOT here
    /// (Twist, 2026-10-13): out cold, you cannot call the police, and an
    /// officer cannot call for backup.
    ///
    /// :startwork is here so City Government staff can go on duty out cold
    /// (Twist, 2026-10-22); ShiftManager.StartShift refuses everybody else,
    /// since only it knows the job.
    /// </summary>
    private static readonly HashSet<string> KnockedOutCan = new(StringComparer.OrdinalIgnoreCase)
    {
        "stats", "bal", "flagme",
        "ga", "ca", "pa",
        "startwork"
    };

    /// <summary>
    /// pixelrp: the other side of it - the player commands that still work ON
    /// somebody who is out cold (Twist, 2026-10-18). Every other one is
    /// refused with <see cref="KnockedOut.TargetRefusal"/>. :escort is how a
    /// patient is carried and put down (a custody escort of somebody out cold
    /// is refused by the command itself); :charge and :pardon are paperwork
    /// that never touches the avatar. Staff commands are not player commands,
    /// so they are never refused here.
    /// </summary>
    private static readonly HashSet<string> TargetKnockedOutCan = new(StringComparer.OrdinalIgnoreCase)
    {
        "escort", "charge", "pardon"
    };

    /// <summary>
    /// pixelrp :invisible: the player commands that still work while you're
    /// invisible (Invisibility) - your own stats and balance, and clocking in
    /// and out. Everything else in the roleplay is refused: nobody is hit,
    /// cuffed or paid by someone they can't see. Staff commands are not player
    /// commands, so they all still work.
    /// </summary>
    private static readonly HashSet<string> InvisibleCan = new(StringComparer.OrdinalIgnoreCase)
    {
        "stats", "bal", "startwork", "stopwork"
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
        // pixelrp: any case and a stray trailing space, as every other command's
        // key is read - ":Commands" (a capitalised first letter) went out as chat.
        if (message.TrimEnd().Equals($"{_prefix}commands", StringComparison.OrdinalIgnoreCase))
        {
            // The caller's commands, grouped for the :commands window's side
            // rail (CommandListLayout). The client reads it as a MOTD that
            // starts with the layout's marker line, so no other MOTD changes.
            var usable = _commands.Values.Where(cmd =>
                string.IsNullOrEmpty(cmd.PermissionRequired) || session.GetHabbo().Permissions.HasCommand(cmd.PermissionRequired));
            session.Send(new MotdNotificationComposer(CommandListLayout.Build(usable, session.GetHabbo().Rank >= CommandListLayout.StaffRank)));
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
            // pixelrp :invisible: someone out of sight is out of the roleplay
            // (InvisibleCan).
            if (IsPlayerCommand(command) && session.GetHabbo().IsInvisible && !InvisibleCan.Contains(key))
            {
                session.SendWhisper("Turn off :invisible first.");
                return true;
            }
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

                // pixelrp: a bot's name ("bot-Name" - the HUD target's "x" types
                // one) is plainly a name, so it never falls back to the selected
                // player: no player command here acts on a bot.
                var typedBot = Rooms.AI.BotNames.IsBotName(typed);
                if (target?.GetHabbo() == null && !typedBot)
                {
                    var hudTargetId = session.GetHabbo().RpHudTargetId;
                    target = hudTargetId > 0 ? _gameClientManager.GetClientByUserId(hudTargetId) : null;
                }

                if (target?.GetHabbo() == null)
                {
                    // Nothing typed (or only "x") and nobody selected; or a name
                    // that is not online, with nobody selected to fall back on.
                    session.SendWhisper(typed == null || typedX ? targetChatCommand.NoTargetMessage
                        : typedBot ? "You can't use this command on a bot."
                        : $"User {typed} seems to be offline.");
                    return true;
                }

                var targetHabbo = target.GetHabbo();
                // pixelrp :invisible: to a player command of anyone who can't
                // see them, someone out of sight is somewhere else - typed or
                // the HUD target selected before they went. The same answer
                // word for word, so it can't be used to find them out. A
                // command that reaches another room already works on them.
                var outOfSight = IsPlayerCommand(command) && targetHabbo.IsInvisible && !session.GetHabbo().IsInvisible;
                if (targetChatCommand.MustBeInSameRoom && (session.GetHabbo().CurrentRoom != targetHabbo.CurrentRoom || outOfSight))
                {
                    session.SendWhisper(targetChatCommand.IsRanged ? RangeMessages.NotInRoom : $"You must be in the same room as {targetHabbo.Username} to execute this command.");
                    return true;
                }

                // pixelrp: a player command used ON somebody out cold - one
                // check here and one sentence, as for being out cold yourself.
                if (IsPlayerCommand(command) && !TargetKnockedOutCan.Contains(key) && KnockedOut.Is(target))
                {
                    session.SendWhisper(KnockedOut.TargetRefusal);
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
    /// pixelrp: whether <see cref="Parse"/> takes this line as a command rather
    /// than leaving it to be said - the same checks it makes before it runs
    /// anything (a known command the player may use, or :commands), with
    /// nothing run, logged or answered. Flood control counts only what is said
    /// (ChatEvent, ShoutEvent), so a line it calls a command must never reach
    /// the room as chat - an unknown command, or a staff one typed by a player,
    /// is said, and counted.
    /// </summary>
    public bool IsCommand(GameClient session, string message)
    {
        if (session?.GetHabbo() == null || !message.StartsWith(_prefix))
            return false;
        if (message.TrimEnd().Equals($"{_prefix}commands", StringComparison.OrdinalIgnoreCase))
            return true;
        var split = message.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return split.Length > 0 && _commands.TryGetValue(split[0].ToLower(), out var command) &&
               (string.IsNullOrEmpty(command.PermissionRequired) || session.GetHabbo().Permissions.HasCommand(command.PermissionRequired));
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