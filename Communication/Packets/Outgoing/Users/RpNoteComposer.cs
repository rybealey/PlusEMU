using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notes;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: one note in full for one viewer - body and version, the people
/// in it (online / has it open / caret line), and, for the owner, their
/// friends list for the share sheet. Pushed to every collaborator on each
/// save and presence change.
/// </summary>
public class RpNoteComposer : IServerPacket
{
    public record Person(int UserId, string Username, bool Online, bool Editing, int CaretLine);

    private readonly NotesUtility.NoteRow _note;
    private readonly List<Person> _people;
    private readonly List<Person> _friends;

    public uint MessageId => ServerPacketHeader.RpNoteComposer;

    public RpNoteComposer(NotesUtility.NoteRow note, List<Person> people, List<Person> friends)
    {
        _note = note;
        _people = people;
        _friends = friends;
    }

    private static void WritePerson(IOutgoingPacket packet, Person p)
    {
        packet.WriteInteger(p.UserId);
        packet.WriteString(p.Username ?? "");
        packet.WriteInteger(p.Online ? 1 : 0);
        packet.WriteInteger(p.Editing ? 1 : 0);
        packet.WriteInteger(p.CaretLine);
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_note.Id);
        packet.WriteInteger(_note.OwnerId);
        packet.WriteString(_note.OwnerName ?? "");
        packet.WriteInteger(_note.FolderId ?? 0);
        packet.WriteString(_note.Title ?? "");
        packet.WriteString(_note.Body ?? "");
        packet.WriteInteger(_note.Pinned);
        packet.WriteInteger(_note.Version);
        packet.WriteInteger(_note.UpdatedAt);
        packet.WriteString(_note.UpdatedBy ?? "");
        packet.WriteInteger(_people.Count);
        foreach (var p in _people) WritePerson(packet, p);
        packet.WriteInteger(_friends.Count);
        foreach (var p in _friends) WritePerson(packet, p);
    }
}
