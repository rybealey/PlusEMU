using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: pushes the player's saved settings to their client at login, as the
/// single JSON document described in 279_UserPrefs.sql. An empty string means
/// nothing has ever been saved, which the client answers by sending up what its
/// own browser had - so it is deliberately not an empty JSON object, which would
/// mean "saved, with nothing set".
/// </summary>
public class RpPrefsComposer : IServerPacket
{
    private readonly string _prefs;

    public uint MessageId => ServerPacketHeader.RpPrefsComposer;

    public RpPrefsComposer(string prefs)
    {
        _prefs = prefs ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_prefs);
    }
}
