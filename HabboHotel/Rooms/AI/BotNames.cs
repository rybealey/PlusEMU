namespace Plus.HabboHotel.Rooms.AI;

/// <summary>
/// pixelrp: every bot shows in the room as "bot-Name" (Twist, 2026-10-18), and
/// no player may take a name that starts that way (Twist, 2026-10-22) - so a
/// name typed with the prefix can only be a bot's. That is what lets the HUD
/// target's "x" type a bot's name into a command (:wander x), and what
/// RoomUserManager.GetBotByName reads it back by.
///
/// The bots table keeps the plain name; the prefix is display only.
/// </summary>
public static class BotNames
{
    public const string Prefix = "bot-";

    /// <summary>Starts with the prefix - a bot's shown name, never a player's.</summary>
    public static bool IsBotName(string? name) => name != null && name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>The name shown for a bot. Not doubled for one already named that way.</summary>
    public static string Display(string name) => IsBotName(name) ? name : Prefix + name;

    /// <summary>Refused as a player's name (sign-up is the website's NotBotNameRule).</summary>
    public const string PlayerNameRefusal = "Names can't start with \"bot-\".";
}
