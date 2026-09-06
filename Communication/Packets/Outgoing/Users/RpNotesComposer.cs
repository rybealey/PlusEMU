using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notes;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>pixelrp: the viewer's Notes home - their folders (with counts) and every note they own or were shared, as summaries.</summary>
public class RpNotesComposer : IServerPacket
{
    private readonly List<NotesUtility.FolderRow> _folders;
    private readonly List<NotesUtility.NoteSummaryRow> _notes;

    public uint MessageId => ServerPacketHeader.RpNotesComposer;

    public RpNotesComposer(List<NotesUtility.FolderRow> folders, List<NotesUtility.NoteSummaryRow> notes)
    {
        _folders = folders;
        _notes = notes;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_folders.Count);
        foreach (var f in _folders)
        {
            packet.WriteInteger(f.Id);
            packet.WriteString(f.Name ?? "");
            packet.WriteInteger(f.Count);
        }
        packet.WriteInteger(_notes.Count);
        foreach (var n in _notes)
        {
            packet.WriteInteger(n.Id);
            packet.WriteInteger(n.OwnerId);
            packet.WriteString(n.OwnerName ?? "");
            packet.WriteString(n.OwnerFigure ?? "");
            packet.WriteInteger(n.FolderId ?? 0);
            packet.WriteString(n.Title ?? "");
            packet.WriteString(n.Body ?? "");
            packet.WriteInteger(n.Pinned);
            packet.WriteInteger(n.UpdatedAt);
            packet.WriteInteger(n.ShareCount);
        }
    }
}
