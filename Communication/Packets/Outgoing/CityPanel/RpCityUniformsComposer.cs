using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>
/// pixelrp City Panel: the Uniforms tab's wearers - every corporation and its
/// ranks, then the prisoners - each with whether a male and a female uniform
/// is set.
/// </summary>
public class RpCityUniformsComposer : IServerPacket
{
    private readonly List<CityUniformCorp> _corps;
    private readonly (bool Male, bool Female) _prisoner;

    public uint MessageId => ServerPacketHeader.RpCityUniformsComposer;

    public RpCityUniformsComposer(List<CityUniformCorp> corps, (bool Male, bool Female) prisoner)
    {
        _corps = corps;
        _prisoner = prisoner;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_corps.Count);
        foreach (var corp in _corps)
        {
            packet.WriteInteger(corp.Id);
            packet.WriteString(corp.Name);
            packet.WriteInteger(corp.Ranks.Count);
            foreach (var rank in corp.Ranks)
            {
                packet.WriteInteger(rank.Id);
                packet.WriteString(rank.Name);
                packet.WriteBoolean(rank.HasMale);
                packet.WriteBoolean(rank.HasFemale);
            }
        }
        packet.WriteBoolean(_prisoner.Male);
        packet.WriteBoolean(_prisoner.Female);
    }
}
