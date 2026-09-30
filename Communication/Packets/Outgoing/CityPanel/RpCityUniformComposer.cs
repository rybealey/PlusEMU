using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>
/// pixelrp City Panel: one uniform's figure ("" for none) - on open, and after
/// a save, with what to tell the staff member.
/// </summary>
public class RpCityUniformComposer : IServerPacket
{
    private readonly string _kind;
    private readonly int _rankId;
    private readonly string _gender;
    private readonly string _figure;
    private readonly string _notice;

    public uint MessageId => ServerPacketHeader.RpCityUniformComposer;

    public RpCityUniformComposer(string kind, int rankId, string gender, string figure, string notice = "")
    {
        _kind = kind;
        _rankId = rankId;
        _gender = gender;
        _figure = figure ?? "";
        _notice = notice ?? "";
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_kind);
        packet.WriteInteger(_rankId);
        packet.WriteString(_gender);
        packet.WriteString(_figure);
        packet.WriteString(_notice);
    }
}
