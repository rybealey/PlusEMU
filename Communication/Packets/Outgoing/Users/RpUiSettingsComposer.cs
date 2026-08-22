using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: pushes the player's persisted UI settings (chrome color scheme)
/// to their client at login. Empty color = client default.
/// </summary>
public class RpUiSettingsComposer : IServerPacket
{
    private readonly string _chromeColor;

    public uint MessageId => ServerPacketHeader.RpUiSettingsComposer;

    public RpUiSettingsComposer(string chromeColor)
    {
        _chromeColor = chromeColor ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_chromeColor);
    }
}
