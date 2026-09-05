using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notes;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: the owner deletes a note (shares go with it); a collaborator leaves it.</summary>
internal class RpDeleteNoteEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || id <= 0 || !NotesUtility.CanAccess(id, habbo.Id)) return Task.CompletedTask;
        var people = NotesUtility.CollaboratorIds(id);
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (NotesUtility.IsOwner(id, habbo.Id))
            {
                connection.Execute("DELETE FROM `rp_note_shares` WHERE `note_id` = @id", new { id });
                connection.Execute("DELETE FROM `rp_notes` WHERE `id` = @id", new { id });
            }
            else
                connection.Execute("DELETE FROM `rp_note_shares` WHERE `note_id` = @id AND `user_id` = @userId", new { id, userId = habbo.Id });
        }
        NotesUtility.SetOpen(id, habbo.Id, false, -1);
        NotesUtility.SendNotesTo(people);
        NotesUtility.BroadcastNote(id);
        return Task.CompletedTask;
    }
}
