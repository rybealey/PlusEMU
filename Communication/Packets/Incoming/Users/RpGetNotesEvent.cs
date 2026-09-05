using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notes;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: Notes app opened - folders and note summaries; with a note id, that note in full.</summary>
internal class RpGetNotesEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (habbo == null) return Task.CompletedTask;
        var noteId = packet.HasDataRemaining() ? packet.ReadInt() : 0;
        if (noteId > 0)
        {
            if (!NotesUtility.CanAccess(noteId, habbo.Id)) return Task.CompletedTask;
            var composer = NotesUtility.ComposeNote(noteId, habbo.Id);
            if (composer != null) session.Send(composer);
            return Task.CompletedTask;
        }
        NotesUtility.SendNotes(session);
        return Task.CompletedTask;
    }
}
