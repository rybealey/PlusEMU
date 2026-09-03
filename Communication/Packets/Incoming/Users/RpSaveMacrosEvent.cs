using System.Text;
using System.Text.Json;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the client saved its macros (Settings window, Macros tab). The
/// payload is the whole macro document as JSON - see 62_Macros.sql for the
/// shape and why it is stored whole.
///
/// The emulator never interprets a macro; it only stores them so they follow
/// the player to another browser. But it must not store client text verbatim
/// either, so the document is parsed, clamped against the limits below and
/// re-serialised. Anything that fails to parse is dropped entirely rather than
/// partially saved - a half-written macro set is worse than an unchanged one.
/// </summary>
public class RpSaveMacrosEvent : IPacketEvent
{
    /// <summary>
    /// Hard ceiling on the incoming string, checked before parsing so a
    /// hostile client cannot make the server parse megabytes. Comfortably
    /// above the worst legitimate document the limits below allow.
    /// </summary>
    private const int MaxPayloadLength = 16384;

    private const int MaxPresets = 8;
    private const int MaxMacrosPerPreset = 40;
    private const int MaxPresetNameLength = 24;
    private const int MaxBindingLength = 24;
    private const int MaxCommandLength = 128;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var payload = packet.ReadString() ?? "";

        // "" is a legitimate save meaning "I have no macros" - store it as-is
        // so the row exists and the client stops falling back to defaults.
        if (payload.Length == 0)
        {
            habbo.EnsureRpMacrosLoaded();
            habbo.RpMacros = "";
            habbo.SaveRpMacros();
            return Task.CompletedTask;
        }

        if (payload.Length > MaxPayloadLength)
            return Task.CompletedTask;

        var sanitised = Sanitise(payload);
        if (sanitised == null)
            return Task.CompletedTask;

        habbo.EnsureRpMacrosLoaded();
        habbo.RpMacros = sanitised;
        habbo.SaveRpMacros();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Rebuilds the document from scratch, keeping only fields we recognise
    /// and only within the limits. Returns null when the payload is not usable
    /// JSON of the expected shape.
    /// </summary>
    private static string? Sanitise(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var enabled = root.TryGetProperty("enabled", out var enabledElement) &&
                          enabledElement.ValueKind == JsonValueKind.True;

            var active = root.TryGetProperty("active", out var activeElement) && activeElement.ValueKind == JsonValueKind.String
                ? Clean(activeElement.GetString(), MaxPresetNameLength)
                : "";

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteNumber("v", 1);
                writer.WriteBoolean("enabled", enabled);
                writer.WriteString("active", active);
                writer.WriteStartArray("presets");

                if (root.TryGetProperty("presets", out var presets) && presets.ValueKind == JsonValueKind.Array)
                {
                    var presetCount = 0;
                    foreach (var preset in presets.EnumerateArray())
                    {
                        if (presetCount >= MaxPresets)
                            break;
                        if (preset.ValueKind != JsonValueKind.Object)
                            continue;
                        if (!preset.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                            continue;

                        var name = Clean(nameElement.GetString(), MaxPresetNameLength);
                        // A preset with no usable name cannot be selected in
                        // the picker, so it would be dead weight in the row.
                        if (name.Length == 0)
                            continue;

                        presetCount++;
                        writer.WriteStartObject();
                        writer.WriteString("name", name);
                        writer.WriteStartArray("macros");

                        if (preset.TryGetProperty("macros", out var macros) && macros.ValueKind == JsonValueKind.Array)
                        {
                            var macroCount = 0;
                            foreach (var macro in macros.EnumerateArray())
                            {
                                if (macroCount >= MaxMacrosPerPreset)
                                    break;
                                if (macro.ValueKind != JsonValueKind.Object)
                                    continue;
                                if (!macro.TryGetProperty("b", out var bindingElement) || bindingElement.ValueKind != JsonValueKind.String)
                                    continue;
                                if (!macro.TryGetProperty("c", out var commandElement) || commandElement.ValueKind != JsonValueKind.String)
                                    continue;

                                var binding = Clean(bindingElement.GetString(), MaxBindingLength);
                                var command = Clean(commandElement.GetString(), MaxCommandLength);
                                // Half a macro does nothing but take up a row
                                // in the list, so drop it.
                                if (binding.Length == 0 || command.Length == 0)
                                    continue;

                                macroCount++;
                                writer.WriteStartObject();
                                writer.WriteString("b", binding);
                                writer.WriteString("c", command);
                                writer.WriteEndObject();
                            }
                        }

                        writer.WriteEndArray();
                        writer.WriteEndObject();
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Trims, drops control characters (they would corrupt the stored document
    /// and could smuggle newlines into a command) and truncates to length.
    /// </summary>
    private static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!char.IsControl(character))
                builder.Append(character);
        }

        var cleaned = builder.ToString().Trim();
        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }
}
