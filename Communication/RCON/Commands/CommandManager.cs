using System.Text.Json;

namespace Plus.Communication.RCON.Commands;

public class CommandManager : ICommandManager
{
    /// <summary>
    /// Commands registered for use.
    /// </summary>
    private readonly Dictionary<string, IRconCommand> _commands;

    /// <summary>
    /// The default initializer for the CommandManager
    /// </summary>
    public CommandManager(IEnumerable<IRconCommand> commands)
    {
        _commands = commands.ToDictionary(command => command.Key);
    }

    /// <summary>
    /// Request the text to parse and check for commands that need to be executed.
    /// </summary>
    /// <param name="data">A string of data split by char(1), the first part being the command and the second part being the parameters.</param>
    /// <returns>True if parsed or false if not.</returns>
    public bool Parse(string data)
    {
        if (data.Length == 0 || string.IsNullOrEmpty(data))
            return false;
        var cmd = data.Split(Convert.ToChar(1))[0];
        if (_commands.TryGetValue(cmd.ToLower(), out var command))
        {
            string[] parameters = null;
            if (data.Split(Convert.ToChar(1))[1] != null)
            {
                var param = data.Split(Convert.ToChar(1))[1];
                parameters = param.Split(':');
            }
            return command.TryExecute(parameters).Result;
        }
        return false;
    }

    /// <summary>
    /// Parses a CMS-dialect JSON RCON request (<c>{"key": "...", "data": {...}}</c>), maps the CMS
    /// command name and its positional parameters onto a registered <see cref="IRconCommand"/>, executes
    /// it, and returns a response that mirrors what <c>RconService::parseResponse</c> on the CMS side
    /// accepts: a decodable object with an integer <c>status</c> (0 = success) and a string <c>message</c>.
    /// </summary>
    public bool ParseJson(string payload, out string response)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException exception)
        {
            response = BuildResponse(1, $"Malformed JSON payload: {exception.Message}");
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("key", out var keyElement) ||
                keyElement.ValueKind != JsonValueKind.String)
            {
                response = BuildResponse(1, "Missing or invalid 'key'");
                return false;
            }

            var cmsKey = keyElement.GetString()?.ToLowerInvariant() ?? string.Empty;
            var hasData = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object;

            string pluCommandKey;
            string[] parameters;

            switch (cmsKey)
            {
                case "givepoints":
                {
                    if (!hasData ||
                        !TryGetPositionalString(data, "user_id", out var userId) ||
                        !TryGetPositionalString(data, "points", out var points) ||
                        !TryGetCurrencyName(data, out var currencyName))
                    {
                        response = BuildResponse(1, "Invalid parameters for 'givepoints'");
                        return false;
                    }

                    pluCommandKey = "give_user_currency";
                    parameters = new[] { userId, currencyName, points };
                    break;
                }
                case "givecredits":
                {
                    if (!hasData ||
                        !TryGetPositionalString(data, "user_id", out var userId) ||
                        !TryGetPositionalString(data, "credits", out var credits))
                    {
                        response = BuildResponse(1, "Invalid parameters for 'givecredits'");
                        return false;
                    }

                    pluCommandKey = "give_user_currency";
                    parameters = new[] { userId, "credits", credits };
                    break;
                }
                case "alertuser":
                {
                    if (!hasData ||
                        !TryGetPositionalString(data, "user_id", out var userId) ||
                        !TryGetPositionalString(data, "message", out var message))
                    {
                        response = BuildResponse(1, "Invalid parameters for 'alertuser'");
                        return false;
                    }

                    pluCommandKey = "alert_user";
                    parameters = new[] { userId, message };
                    break;
                }
                case "disconnect":
                {
                    if (!hasData || !TryGetPositionalString(data, "user_id", out var userId))
                    {
                        response = BuildResponse(1, "Invalid parameters for 'disconnect'");
                        return false;
                    }

                    pluCommandKey = "disconnect_user";
                    parameters = new[] { userId };
                    break;
                }
                case "givepassive":
                {
                    if (!hasData ||
                        !TryGetPositionalString(data, "user_id", out var userId) ||
                        !TryGetPositionalString(data, "seconds", out var seconds))
                    {
                        response = BuildResponse(1, "Invalid parameters for 'givepassive'");
                        return false;
                    }

                    pluCommandKey = "give_user_passive";
                    parameters = new[] { userId, seconds };
                    break;
                }
                default:
                    response = BuildResponse(1, $"Unknown RCON command '{cmsKey}'");
                    return false;
            }

            if (!_commands.TryGetValue(pluCommandKey, out var command))
            {
                response = BuildResponse(1, $"Command '{pluCommandKey}' is not registered");
                return false;
            }

            bool success;
            try
            {
                success = command.TryExecute(parameters).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                response = BuildResponse(1, $"Command '{pluCommandKey}' threw: {exception.Message}");
                return false;
            }

            response = success
                ? BuildResponse(0, "OK")
                : BuildResponse(1, $"Command '{pluCommandKey}' failed to execute");
            return success;
        }
    }

    /// <summary>
    /// Reads a JSON property as its raw string representation, coercing both JSON string and JSON
    /// number values (the CMS may send either depending on the field).
    /// </summary>
    private static bool TryGetPositionalString(JsonElement obj, string propertyName, out string value)
    {
        value = null;
        if (!obj.TryGetProperty(propertyName, out var element))
            return false;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                value = element.GetString();
                return !string.IsNullOrEmpty(value);
            case JsonValueKind.Number:
                value = element.GetRawText();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Resolves the CMS's <c>CurrencyTypes</c> value (a backed int enum, or occasionally its string
    /// name) to the currency name PlusEMU's <c>give_user_currency</c> command expects.
    /// </summary>
    private static bool TryGetCurrencyName(JsonElement data, out string currencyName)
    {
        currencyName = null;
        if (!data.TryGetProperty("type", out var typeElement))
            return false;

        if (typeElement.ValueKind == JsonValueKind.String)
        {
            currencyName = typeElement.GetString()?.ToLowerInvariant() switch
            {
                "diamonds" => "diamonds",
                "duckets" => "duckets",
                "credits" => "credits",
                _ => null
            };
            return currencyName != null;
        }

        if (typeElement.ValueKind == JsonValueKind.Number && typeElement.TryGetInt32(out var typeInt))
        {
            currencyName = typeInt switch
            {
                5 => "diamonds",
                0 => "duckets",
                -1 => "credits",
                _ => null
            };
            return currencyName != null;
        }

        return false;
    }

    private static string BuildResponse(int status, string message) =>
        JsonSerializer.Serialize(new { status, message });
}