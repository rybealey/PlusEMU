using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: pushes the player's persisted UI settings (chrome color scheme)
/// to their client at login. Empty color = client default.
/// </summary>
public class RpUiSettingsComposer : IServerPacket
{
    private readonly string _chromeColor;
    private readonly int _chromeOpacity;
    private readonly string _headerColor;
    private readonly string _usernameColor;
    private readonly string _icon;
    private readonly string _iconColor;

    public uint MessageId => ServerPacketHeader.RpUiSettingsComposer;

    public RpUiSettingsComposer(string chromeColor, int chromeOpacity, string headerColor, string usernameColor, string icon, string iconColor)
    {
        _chromeColor = chromeColor ?? "";
        _chromeOpacity = chromeOpacity;
        _headerColor = headerColor ?? "";
        _usernameColor = usernameColor ?? "";
        _icon = icon ?? "";
        _iconColor = iconColor ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_chromeColor);
        packet.WriteInteger(_chromeOpacity);
        packet.WriteString(_headerColor);
        packet.WriteString(_usernameColor);
        packet.WriteString(_icon);
        packet.WriteString(_iconColor);
    }
}
