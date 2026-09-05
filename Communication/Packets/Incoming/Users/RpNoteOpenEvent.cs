using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notes;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: presence - a note opened (1) or closed (0) in the editor, with the caret's line.</summary>
internal class RpNoteOpenEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var open = packet.ReadInt() == 1;
        var caretLine = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || id <= 0 || !NotesUtility.CanAccess(id, habbo.Id)) return Task.CompletedTask;
        NotesUtility.SetOpen(id, habbo.Id, open, caretLine);
        NotesUtility.BroadcastNote(id);
        return Task.CompletedTask;
    }
}
