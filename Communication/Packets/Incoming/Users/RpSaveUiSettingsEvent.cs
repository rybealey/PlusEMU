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

    // fa-style icon classes only (e.g. "fa-kit fa-kit-star"); empty = none.
    // Strict allowlist blocks arbitrary class/HTML injection over the wire.
    [GeneratedRegex("^[a-z0-9][a-z0-9 -]{0,63}$")]
    private static partial Regex IconClass();

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var color = packet.ReadString() ?? "";
        var opacity = packet.ReadInt();
        var header = packet.ReadString() ?? "";
        var usernameColor = packet.ReadString() ?? "";
        var icon = packet.ReadString() ?? "";
        var iconColor = packet.ReadString() ?? "";
        if (header != "" && header != "orange" && header != "pink" && header != "purple")
            return Task.CompletedTask;
        if (color != "" && !HexColor().IsMatch(color))
            return Task.CompletedTask;
        if (usernameColor != "" && !HexColor().IsMatch(usernameColor))
            return Task.CompletedTask;
        if (iconColor != "" && !HexColor().IsMatch(iconColor))
            return Task.CompletedTask;
        if (icon != "" && !IconClass().IsMatch(icon))
            return Task.CompletedTask;
        // pixelrp: image icons named vip-*.png (stored as "img-vip-...") are
        // VIP-exclusive.
        if (icon.StartsWith("img-vip-") && !session.GetHabbo().IsVip)
            return Task.CompletedTask;
        var habbo = session.GetHabbo();
        if (habbo == null)
            return Task.CompletedTask;
        habbo.EnsureRpUiSettingsLoaded();
        habbo.RpUiChromeColor = color;
        habbo.RpUiChromeOpacity = Math.Clamp(opacity, 40, 100);
        habbo.RpUiHeaderColor = header;
        habbo.RpUiUsernameColor = usernameColor;
        habbo.RpUiUsernameIcon = icon;
        habbo.RpUiUsernameIconColor = iconColor;
        habbo.SaveRpUiSettings();
        return Task.CompletedTask;
    }
}
