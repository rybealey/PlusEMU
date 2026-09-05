using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notes;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: file a note into one of YOUR folders (0 = none). Owner via rp_notes, collaborator via their share row.</summary>
internal class RpMoveNoteEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var folderId = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null || id <= 0 || !NotesUtility.CanAccess(id, habbo.Id)) return Task.CompletedTask;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            int? folder = folderId > 0 && connection.QueryFirstOrDefault<int?>("SELECT `id` FROM `rp_note_folders` WHERE `id` = @folderId AND `user_id` = @userId", new { folderId, userId = habbo.Id }) != null ? folderId : null;
            if (NotesUtility.IsOwner(id, habbo.Id))
                connection.Execute("UPDATE `rp_notes` SET `folder_id` = @folder WHERE `id` = @id", new { id, folder });
            else
                connection.Execute("UPDATE `rp_note_shares` SET `folder_id` = @folder WHERE `note_id` = @id AND `user_id` = @userId", new { id, folder, userId = habbo.Id });
        }
        NotesUtility.SendNotes(session);
        return Task.CompletedTask;
    }
}
