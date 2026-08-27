namespace Plus.Communication.RCON.Commands;

public interface ICommandManager
{
    /// <summary>
    /// Request the text to parse and check for commands that need to be executed.
    /// </summary>
    /// <param name="data">A string of data split by char(1), the first part being the command and the second part being the parameters.</param>
    /// <returns>True if parsed or false if not.</returns>
    bool Parse(string data);

    /// <summary>
    /// Parses a CMS-dialect JSON RCON request (<c>{"key": "...", "data": {...}}</c>), maps it onto a
    /// registered <see cref="IRconCommand"/>, and executes it.
    /// </summary>
    /// <param name="payload">The raw JSON payload received on the socket.</param>
    /// <param name="response">
    /// A single JSON line (<c>{"status": 0|1, "message": "..."}</c>) suitable for
    /// <c>RconService::parseResponse</c> on the CMS side. Always populated, even on failure.
    /// </param>
    /// <returns>True if the command was found and executed successfully.</returns>
    bool ParseJson(string payload, out string response);
}