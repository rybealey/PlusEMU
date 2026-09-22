using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.News;
using Plus.HabboHotel.Support;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the Support app as ONE PLAYER sees it.
///
/// Every thread is with Trina - the News app's newsroom byline (NewsUtility) -
/// and this composer is where that becomes true on the wire. It writes the
/// byline's id, name and figure once, and then NOTHING about the staff member
/// actually holding any thread: not their id, not their name, not whether the
/// thread has changed hands. The staff view has its own composer for that.
///
/// That omission is the feature, not an oversight. A silent reassignment is
/// only silent because there is no field here that could carry it.
/// </summary>
public class RpSupportComposer : IServerPacket
{
    private readonly NewsUtility.BylineRow _byline;
    private readonly int _availableStaff;
    private readonly List<SupportUtility.ThreadRow> _threads;
    private readonly Dictionary<int, SupportUtility.MessageRow> _previews;
    private readonly int _openThreadId;
    private readonly List<SupportUtility.MessageRow> _messages;

    public uint MessageId => ServerPacketHeader.RpSupportComposer;

    public RpSupportComposer(NewsUtility.BylineRow byline, int availableStaff,
        List<SupportUtility.ThreadRow> threads, Dictionary<int, SupportUtility.MessageRow> previews,
        int openThreadId, List<SupportUtility.MessageRow> messages)
    {
        _byline = byline;
        _availableStaff = availableStaff;
        _threads = threads;
        _previews = previews;
        _openThreadId = openThreadId;
        _messages = messages;
    }

    public void Compose(IOutgoingPacket packet)
    {
        // Who the player is talking to, always and only.
        packet.WriteInteger(_byline.Id);
        packet.WriteString(_byline.Username ?? "");
        packet.WriteString(_byline.Figure ?? "");
        // "Trina is online now" - availability, not a staff roster. A count is
        // the most the player learns about who is behind her.
        packet.WriteInteger(_availableStaff > 0 ? 1 : 0);

        packet.WriteInteger(_threads.Count);
        foreach (var thread in _threads)
        {
            packet.WriteInteger(thread.Id);
            packet.WriteString(thread.Category ?? "other");
            // 'offered' is collapsed into 'waiting': from the player's side
            // being held for somebody and waiting for anybody are the same
            // thing, and telling them apart is exactly what would reveal the
            // rotation.
            packet.WriteString(thread.Status == "resolved" ? "resolved"
                : thread.Status == "open" ? "open" : "waiting");
            packet.WriteInteger(thread.CreatedAt);
            packet.WriteInteger(thread.UpdatedAt);
            var hasPreview = _previews.TryGetValue(thread.Id, out var preview);
            packet.WriteString(hasPreview ? (preview.Body ?? "") : "");
            packet.WriteInteger(hasPreview && preview.FromStaff == 1 ? 1 : 0);
        }

        packet.WriteInteger(_openThreadId);
        packet.WriteInteger(_messages.Count);
        foreach (var message in _messages)
        {
            packet.WriteInteger(message.Id);
            // Which SIDE, never who. A staff line is Trina's line here.
            packet.WriteInteger(message.FromStaff);
            packet.WriteString(message.Body ?? "");
            packet.WriteInteger(message.CreatedAt);
        }
    }
}
