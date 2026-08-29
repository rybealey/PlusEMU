using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the corporations directory - every corporation with its badge and
/// employee headcount, for the Corporations window's rail.
/// </summary>
public class RpCorpsComposer : IServerPacket
{
    public record CorpEntry(int Id, string Name, string Badge, int Employees);

    private readonly List<CorpEntry> _corps;

    public uint MessageId => ServerPacketHeader.RpCorpsComposer;

    public RpCorpsComposer(List<CorpEntry> corps)
    {
        _corps = corps;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_corps.Count);
        foreach (var corp in _corps)
        {
            packet.WriteInteger(corp.Id);
            packet.WriteString(corp.Name ?? "");
            packet.WriteString(corp.Badge ?? "");
            packet.WriteInteger(corp.Employees);
        }
    }
}
