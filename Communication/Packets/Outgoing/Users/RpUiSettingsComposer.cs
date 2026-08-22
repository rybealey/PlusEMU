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

    public uint MessageId => ServerPacketHeader.RpUiSettingsComposer;

    public RpUiSettingsComposer(string chromeColor, int chromeOpacity)
    {
        _chromeColor = chromeColor ?? "";
        _chromeOpacity = chromeOpacity;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_chromeColor);
        packet.WriteInteger(_chromeOpacity);
    }
}
