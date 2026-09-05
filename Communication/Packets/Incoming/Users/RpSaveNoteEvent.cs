using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notes;
using Plus.HabboHotel.Rooms.Chat.Filter;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>
/// pixelrp: create (id 0, into folderId) or save a note. Last writer wins:
/// the version bumps and the full note goes to every online collaborator;
/// summaries follow so lists and previews stay current. caretLine keeps the
/// editor's presence fresh.
/// </summary>
internal class RpSaveNoteEvent : IPacketEvent
{
    private readonly IWordFilterManager _wordFilterManager;

    public RpSaveNoteEvent(IWordFilterManager wordFilterManager)
    {
        _wordFilterManager = wordFilterManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var folderId = packet.ReadInt();
        var title = _wordFilterManager.CheckMessage(packet.ReadString());
        var body = _wordFilterManager.CheckMessage(packet.ReadString());
        var caretLine = packet.ReadInt();
        var habbo = session.GetHabbo();
        if (habbo == null) return Task.CompletedTask;

        if (title.Length > NotesUtility.MaxTitle) title = title.Substring(0, NotesUtility.MaxTitle);
        if (body.Length > NotesUtility.MaxBody) body = body.Substring(0, NotesUtility.MaxBody);
        var now = NotesUtility.Now();

        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (id <= 0)
            {
                var count = connection.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM `rp_notes` WHERE `owner_id` = @userId", new { userId = habbo.Id });
                if (count >= NotesUtility.MaxNotes)
                {
                    session.SendWhisper($"You can keep up to {NotesUtility.MaxNotes} notes.");
                    return Task.CompletedTask;
                }
                // the folder must be the owner's
                int? folder = folderId > 0 && connection.QueryFirstOrDefault<int?>("SELECT `id` FROM `rp_note_folders` WHERE `id` = @folderId AND `user_id` = @userId", new { folderId, userId = habbo.Id }) != null ? folderId : null;
                id = connection.QuerySingle<int>(
                    "INSERT INTO `rp_notes` (`owner_id`, `folder_id`, `title`, `body`, `pinned`, `version`, `created_at`, `updated_at`, `updated_by`) " +
                    "VALUES (@userId, @folder, @title, @body, 0, 1, @now, @now, @userId); SELECT LAST_INSERT_ID();",
                    new { userId = habbo.Id, folder, title, body, now });
            }
            else
            {
                if (!NotesUtility.CanAccess(id, habbo.Id)) return Task.CompletedTask;
                connection.Execute(
                    "UPDATE `rp_notes` SET `title` = @title, `body` = @body, `version` = `version` + 1, `updated_at` = @now, `updated_by` = @userId WHERE `id` = @id",
                    new { id, title, body, now, userId = habbo.Id });
            }
        }

        NotesUtility.SetOpen(id, habbo.Id, true, caretLine);
        NotesUtility.BroadcastNote(id);
        NotesUtility.SendNotesTo(NotesUtility.CollaboratorIds(id));
        return Task.CompletedTask;
    }
}
