using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: one corporation in full - ranks (lowest order first; the client
/// presents highest first) with pay per 10-minute shift interval and the
/// employees at each rank (tier I-V, online / on-duty state).
/// </summary>
public class RpCorpDetailComposer : IServerPacket
{
    public record Employee(string Username, string Figure, int Tier, bool Online, bool OnDuty);

    public record Rank(int Id, int Order, string Name, int Pay, int Tiers, List<Employee> Employees);

    private readonly int _corpId;
    private readonly string _name;
    private readonly string _badge;
    private readonly string _description;
    private readonly List<Rank> _ranks;

    public uint MessageId => ServerPacketHeader.RpCorpDetailComposer;

    public RpCorpDetailComposer(int corpId, string name, string badge, string description, List<Rank> ranks)
    {
        _corpId = corpId;
        _name = name;
        _badge = badge;
        _description = description;
        _ranks = ranks;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_corpId);
        packet.WriteString(_name ?? "");
        packet.WriteString(_badge ?? "");
        packet.WriteString(_description ?? "");
        packet.WriteInteger(_ranks.Sum(rank => rank.Employees.Count));
        packet.WriteInteger(_ranks.Count);
        foreach (var rank in _ranks)
        {
            packet.WriteInteger(rank.Id);
            packet.WriteInteger(rank.Order);
            packet.WriteString(rank.Name ?? "");
            packet.WriteInteger(rank.Pay);
            packet.WriteInteger(rank.Tiers);
            packet.WriteInteger(rank.Employees.Count);
            foreach (var employee in rank.Employees)
            {
                packet.WriteString(employee.Username ?? "");
                packet.WriteString(employee.Figure ?? "");
                packet.WriteInteger(employee.Tier);
                packet.WriteInteger(employee.Online ? 1 : 0);
                packet.WriteInteger(employee.OnDuty ? 1 : 0);
            }
        }
    }
}
