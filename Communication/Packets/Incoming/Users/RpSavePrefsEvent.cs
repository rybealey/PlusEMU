using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the client saved its settings (279_UserPrefs.sql) - the whole
/// document, a JSON map from each setting's storage key to its value.
///
/// Like the macros, the emulator never reads a setting; it only keeps them so
/// they follow the player to another browser. But it must not store client
/// text verbatim either, so the document is parsed, only the keys below are
/// kept, each value is clamped, and it is written out again. A payload that
/// fails to parse is dropped whole - a half-saved set of settings is worse
/// than an unchanged one.
/// </summary>
public class RpSavePrefsEvent : IPacketEvent
{
    /// <summary>Checked before parsing, so a hostile client cannot make the server parse megabytes.</summary>
    private const int MaxPayloadLength = 32768;

    private const int MaxKeys = 256;

    /// <summary>Well past the longest setting (the HUD colours, a few hundred characters).</summary>
    private const int MaxValueLength = 4096;

    /// <summary>
    /// Every setting the account keeps - the client's AccountPrefs holds the
    /// same list. Kept in step by hand: a key missing here is simply not saved.
    /// </summary>
    private static readonly HashSet<string> Keys = new(StringComparer.Ordinal)
    {
        // Settings > General
        "pixelrp.prefs.fps.max",
        "pixelrp.prefs.fps.slow",
        "pixelrp.prefs.room.drag",
        "pixelrp.prefs.clock24",
        "pixelrp.prefs.celsius",
        "pixelrp.ping-volume",
        "pixelrp.music.volume",
        "pixelrp.prefs.phone.hold",
        // Settings > UI
        "pixelrp.prefs.hud",
        "pixelrp.prefs.colours.saved",
        "pixelrp.prefs.iconTint",
        "pixelrp.prefs.cursor.style",
        "pixelrp.prefs.cursor.colour",
        "pixelrp.environment.weather",
        // the music player and the profile song
        "pixelrp.jukebox.volume",
        "pixelrp.jukebox.muted",
        "pixelrp.jukebox.roompaused",
        "pixelrp.song.volume",
        "pixelrp.song.muted",
        // panels left open or closed
        "pixelrp.side-drawer.expanded",
        "pixelrp.turf-panel.expanded",
        "pixelrp.backpack.item-use-mode",
        // the screen the window positions were saved on
        "nitro.screensize"
    };

    /// <summary>Where each window was last dragged to, and its size: nitro.windows.&lt;window&gt;.</summary>
    private static readonly Regex WindowKey = new(@"^nitro\.windows\.[A-Za-z0-9_.-]{1,64}$", RegexOptions.Compiled);

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var payload = packet.ReadString() ?? "";
        if (payload.Length == 0 || payload.Length > MaxPayloadLength)
            return Task.CompletedTask;

        var sanitised = Sanitise(payload);
        if (sanitised == null)
            return Task.CompletedTask;

        habbo.EnsureRpPrefsLoaded();
        habbo.RpPrefs = sanitised;
        habbo.SaveRpPrefs();
        return Task.CompletedTask;
    }

    private static bool IsKey(string key) => Keys.Contains(key) || WindowKey.IsMatch(key);

    /// <summary>
    /// Rebuilds the document from scratch with only the known keys whose value
    /// is a string within the limit. Returns null when the payload is not a JSON
    /// object.
    /// </summary>
    private static string? Sanitise(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                var count = 0;
                foreach (var property in root.EnumerateObject())
                {
                    if (count >= MaxKeys)
                        break;
                    if (!IsKey(property.Name) || property.Value.ValueKind != JsonValueKind.String)
                        continue;

                    var value = Clean(property.Value.GetString());
                    if (value.Length > MaxValueLength)
                        continue;

                    count++;
                    writer.WriteString(property.Name, value);
                }
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Drops control characters, which no setting holds and which would only corrupt the stored document.</summary>
    private static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (!char.IsControl(c))
                builder.Append(c);
        }
        return builder.ToString();
    }
}
