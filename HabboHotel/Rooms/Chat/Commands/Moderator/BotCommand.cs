using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

internal class BotCommand : IChatCommand
{
    private readonly IDatabase _database;
    public string Key => "bot";
    public string PermissionRequired => "command_bot";

    public string Parameters => "%on/off%";

    public string Description => "Turn the Claude bot on or off, or check its state.";

    public BotCommand(IDatabase database)
    {
        _database = database;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        // The emulator never consumes this flag itself; the bot process polls the
        // server_settings row (~10s) and connects/disconnects accordingly, so state
        // changes here take effect within its poll interval.
        var argument = parameters.Length > 0 ? parameters[0].ToLower() : "";
        if (argument != "on" && argument != "off")
        {
            session.SendWhisper($"The Claude bot is currently {(ReadFlag() ? "ON" : "OFF")}. Use :bot on or :bot off to change it.");
            return;
        }
        var enable = argument == "on";
        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("INSERT INTO `server_settings` (`key`, `value`, `description`) VALUES ('bot.enabled', @value, 'Claude bot kill switch, toggled in-game with :bot on/off.') ON DUPLICATE KEY UPDATE `value` = @value");
            dbClient.AddParameter("value", enable ? "1" : "0");
            dbClient.RunQuery();
        }
        session.SendWhisper(enable
            ? "Claude bot turned ON - it will reconnect within a few seconds."
            : "Claude bot turned OFF - it will disconnect within a few seconds.");
    }

    private bool ReadFlag()
    {
        using var dbClient = _database.GetQueryReactor();
        dbClient.SetQuery("SELECT `value` FROM `server_settings` WHERE `key` = 'bot.enabled' LIMIT 1");
        var value = dbClient.GetString();
        // No row yet means the seed hasn't run; the bot treats that as enabled too.
        return string.IsNullOrEmpty(value) || value == "1";
    }
}
