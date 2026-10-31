using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Corporations;

/// <summary>
/// pixelrp Corporation Panel: Promo requests and Demotion requests - whether
/// the viewer approves, the weekly quota, every open request and those decided
/// in the last week (with what would stop this viewer approving one), and
/// every employee with the step above and below them, whether the viewer may
/// ask for each, and their week so far. The client splits it by direction.
/// </summary>
public class RpCorpPanelRequestsComposer : IServerPacket
{
    public record Request(int Id, int UserId, string Username, string Look, string Gender, string Direction, string FromTitle, string ToTitle,
        string Reason, int RequestedBy, string RequestedByName, string Status, string DecidedByName, int DecidedAt, string Note, int CreatedAt,
        string Blocker);

    public record Person(int UserId, string Username, string Look, string Gender, string RankTitle, string UpTitle, string DownTitle,
        bool CanRequestUp, bool CanRequestDown, int WeekSeconds, int Warnings, int Strikes, int InRankSince);

    private readonly bool _canApprove;
    private readonly int _shiftQuota;
    private readonly List<Request> _requests;
    private readonly List<Person> _people;

    public uint MessageId => ServerPacketHeader.RpCorpPanelRequestsComposer;

    public RpCorpPanelRequestsComposer(bool canApprove, int shiftQuota, List<Request> requests, List<Person> people)
    {
        _canApprove = canApprove;
        _shiftQuota = shiftQuota;
        _requests = requests;
        _people = people;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(_canApprove);
        packet.WriteInteger(_shiftQuota);
        packet.WriteInteger(_requests.Count);
        foreach (var request in _requests)
        {
            packet.WriteInteger(request.Id);
            packet.WriteInteger(request.UserId);
            packet.WriteString(request.Username ?? "");
            packet.WriteString(request.Look ?? "");
            packet.WriteString(request.Gender ?? "");
            packet.WriteString(request.Direction ?? "");
            packet.WriteString(request.FromTitle ?? "");
            packet.WriteString(request.ToTitle ?? "");
            packet.WriteString(request.Reason ?? "");
            packet.WriteInteger(request.RequestedBy);
            packet.WriteString(request.RequestedByName ?? "");
            packet.WriteString(request.Status ?? "");
            packet.WriteString(request.DecidedByName ?? "");
            packet.WriteInteger(request.DecidedAt);
            packet.WriteString(request.Note ?? "");
            packet.WriteInteger(request.CreatedAt);
            packet.WriteString(request.Blocker ?? "");
        }
        packet.WriteInteger(_people.Count);
        foreach (var person in _people)
        {
            packet.WriteInteger(person.UserId);
            packet.WriteString(person.Username ?? "");
            packet.WriteString(person.Look ?? "");
            packet.WriteString(person.Gender ?? "");
            packet.WriteString(person.RankTitle ?? "");
            packet.WriteString(person.UpTitle ?? "");
            packet.WriteString(person.DownTitle ?? "");
            packet.WriteBoolean(person.CanRequestUp);
            packet.WriteBoolean(person.CanRequestDown);
            packet.WriteInteger(person.WeekSeconds);
            packet.WriteInteger(person.Warnings);
            packet.WriteInteger(person.Strikes);
            packet.WriteInteger(person.InRankSince);
        }
    }
}
