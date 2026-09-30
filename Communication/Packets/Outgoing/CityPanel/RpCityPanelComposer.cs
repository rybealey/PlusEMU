using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.CityPanel;

/// <summary>
/// pixelrp City Panel: the answer to opening it - which actions this staff
/// member may offer (CityPanelAccess.Capability bits; the server checks each
/// again) and the items the backpack's Give can hand out.
/// </summary>
public class RpCityPanelComposer : IServerPacket
{
    private readonly int _capabilities;
    private readonly List<(string ItemKey, string Name)> _items;

    public uint MessageId => ServerPacketHeader.RpCityPanelComposer;

    public RpCityPanelComposer(CityPanelAccess.Capability capabilities, IEnumerable<(string ItemKey, string Name)> items)
    {
        _capabilities = (int)capabilities;
        _items = items.ToList();
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_capabilities);
        packet.WriteInteger(_items.Count);
        foreach (var (key, name) in _items)
        {
            packet.WriteString(key);
            packet.WriteString(name);
        }
    }
}
