using System.Text.RegularExpressions;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: the client saved a UI setting (chrome color scheme picked in the
/// Settings window's Interface tab). Persists per user; "" resets to default.
/// </summary>
public partial class RpSaveUiSettingsEvent : IPacketEvent
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var color = packet.ReadString() ?? "";
        var opacity = packet.ReadInt();
        if (color != "" && !HexColor().IsMatch(color))
            return Task.CompletedTask;
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        habbo.EnsureRpUiSettingsLoaded();
        habbo.RpUiChromeColor = color;
        habbo.RpUiChromeOpacity = Math.Clamp(opacity, 40, 100);
        habbo.SaveRpUiSettings();
        return Task.CompletedTask;
    }
}
