using System.Text;
using System.Text.Json;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp phone: the client saved one of its two phone documents - see
/// 91_PhoneState.sql for what they are and why they are stored whole.
///
/// The emulator never interprets either document; it only keeps them so the
/// phone follows the player to another browser. Unlike the macros, there is no
/// field whitelist here: the schema is the client's and it versions and
/// migrates it itself, and every field is re-validated on read (usePhone's
/// readPrefs / readNotify / readAccess), so a hand-edited document can only
/// ever affect the player who edited it. What the server does insist on is
/// that the payload IS JSON, IS the right kind of JSON (an object for prefs,
/// an array for notifications), and fits - then it re-serialises it through
/// System.Text.Json so the column never holds client bytes verbatim.
///
/// Anything that fails those checks is dropped rather than partially saved.
/// </summary>
public class RpSavePhoneStateEvent : IPacketEvent
{
    public const int KindPrefs = 0;
    public const int KindNotifications = 1;

    /// <summary>
    /// Ceilings checked BEFORE parsing, so a hostile client cannot make the
    /// server parse megabytes. A real prefs document is under 2 KB; forty
    /// notifications with long subjects run to perhaps 10 KB.
    /// </summary>
    private const int MaxPrefsLength = 32 * 1024;
    private const int MaxNotificationsLength = 64 * 1024;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;

        var kind = packet.ReadInt();
        var payload = packet.ReadString() ?? "";

        switch (kind)
        {
            case KindPrefs:
            {
                if (payload.Length > MaxPrefsLength)
                    return Task.CompletedTask;
                var clean = Normalise(payload, JsonValueKind.Object);
                if (clean == null)
                    return Task.CompletedTask;
                habbo.EnsureRpPhoneLoaded();
                habbo.RpPhonePrefs = clean;
                habbo.SaveRpPhone();
                break;
            }
            case KindNotifications:
            {
                if (payload.Length > MaxNotificationsLength)
                    return Task.CompletedTask;
                var clean = Normalise(payload, JsonValueKind.Array);
                if (clean == null)
                    return Task.CompletedTask;
                habbo.EnsureRpPhoneLoaded();
                habbo.RpPhoneNotifications = clean;
                habbo.SaveRpPhone();
                break;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Parses and re-emits the document, or returns null when it is not JSON
    /// of the expected root kind. "" is accepted as-is: it is the client's
    /// legitimate way of saying "I have nothing" (an emptied Notification
    /// Center saves as []) and, for prefs, is never sent - but storing it
    /// would only mean "never saved", which is the correct reading anyway.
    /// </summary>
    private static string? Normalise(string payload, JsonValueKind expected)
    {
        if (payload.Length == 0)
            return "";

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != expected)
                return null;

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
                document.RootElement.WriteTo(writer);

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
