using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Support;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the Support queue as STAFF see it - the other half of
/// RpSupportComposer, and the only one that carries real names.
///
/// Staff see who opened a thread, who holds it, and how long an offer has left
/// to run. The byline is not used here at all: hiding colleagues from each
/// other would make the rotation impossible to work with, and the anonymity
/// only ever pointed one way.
/// </summary>
public class RpSupportQueueComposer : IServerPacket
{
    private readonly int _available;
    private readonly int _availableStaff;
    private readonly int _viewerId;
    private readonly List<SupportUtility.ThreadRow> _threads;
    private readonly Dictionary<int, SupportUtility.MessageRow> _previews;
    private readonly int _openThreadId;
    private readonly List<SupportUtility.MessageRow> _messages;

    public uint MessageId => ServerPacketHeader.RpSupportQueueComposer;

    public RpSupportQueueComposer(int available, int availableStaff, int viewerId,
        List<SupportUtility.ThreadRow> threads, Dictionary<int, SupportUtility.MessageRow> previews,
        int openThreadId, List<SupportUtility.MessageRow> messages)
    {
        _available = available;
        _availableStaff = availableStaff;
        _viewerId = viewerId;
        _threads = threads;
        _previews = previews;
        _openThreadId = openThreadId;
        _messages = messages;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_available);        // this viewer's own toggle
        packet.WriteInteger(_availableStaff);   // how many are taking chats
        packet.WriteInteger(_viewerId);

        packet.WriteInteger(_threads.Count);
        foreach (var thread in _threads)
        {
            packet.WriteInteger(thread.Id);
            packet.WriteInteger(thread.PlayerId);
            packet.WriteString(thread.PlayerName ?? "");
            packet.WriteString(thread.Category ?? "other");
            packet.WriteString(thread.Status ?? "waiting");
            packet.WriteInteger(thread.StaffId);
            packet.WriteString(thread.StaffName ?? "");
            // Seconds left on the offer, so the client can count down without
            // needing the server's clock. Zero when it is not an offer.
            packet.WriteInteger(thread.OfferedUntil);
            packet.WriteInteger(thread.Offers);
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
            packet.WriteInteger(message.FromStaff);
            packet.WriteInteger(message.AuthorId);
            packet.WriteString(message.Body ?? "");
            packet.WriteInteger(message.CreatedAt);
        }
    }
}
