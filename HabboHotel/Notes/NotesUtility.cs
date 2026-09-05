using System.Collections.Concurrent;
using Dapper;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.Utilities;

namespace Plus.HabboHotel.Notes;

/// <summary>
/// pixelrp: the phone's Notes app. Notes are owned, optionally shared with
/// friends, and filed per person into personal folders. Live sharing is
/// last-writer-wins: every save bumps the version and the full note is pushed
/// to every online collaborator; who has a note open (and which line their
/// caret is on) is tracked in memory and rides along.
/// Row types are property classes for Dapper.
/// </summary>
public static class NotesUtility
{
    public const int MaxTitle = 80;
    public const int MaxBody = 20000;
    public const int MaxFolderName = 32;
    public const int MaxFolders = 20;
    public const int MaxNotes = 200;
    public const int MaxCollaborators = 10;

    public class FolderRow { public int Id { get; set; } public string Name { get; set; } = ""; public int Count { get; set; } }

    public class NoteSummaryRow
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = "";
        public int? FolderId { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public int Pinned { get; set; }
        public int UpdatedAt { get; set; }
        public int ShareCount { get; set; }
    }

    public class NoteRow
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = "";
        public int? FolderId { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public int Pinned { get; set; }
        public int Version { get; set; }
        public int UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = "";
    }

    public class PersonRow { public int UserId { get; set; } public string Username { get; set; } = ""; }

    // noteId -> userId -> caret line, for everyone with the note open
    private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, int>> Open = new();

    public static int Now() => (int)UnixTimestamp.GetNow();

    public static bool IsOnline(int userId) => PlusEnvironment.Game.ClientManager.GetClientByUserId(userId)?.GetHabbo() != null;

    // ---- access -----------------------------------------------------------

    /// <summary>Owner or collaborator.</summary>
    public static bool CanAccess(int noteId, int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<int?>(
            "SELECT 1 FROM `rp_notes` n WHERE n.`id` = @noteId AND (n.`owner_id` = @userId OR EXISTS (SELECT 1 FROM `rp_note_shares` s WHERE s.`note_id` = n.`id` AND s.`user_id` = @userId)) LIMIT 1",
            new { noteId, userId }) != null;
    }

    public static bool IsOwner(int noteId, int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<int?>("SELECT 1 FROM `rp_notes` WHERE `id` = @noteId AND `owner_id` = @userId LIMIT 1", new { noteId, userId }) != null;
    }

    public static bool AreFriends(int a, int b)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<int?>(
            "SELECT 1 FROM `messenger_friendships` WHERE (`user_one_id` = @a AND `user_two_id` = @b) OR (`user_one_id` = @b AND `user_two_id` = @a) LIMIT 1", new { a, b }) != null;
    }

    public static List<int> CollaboratorIds(int noteId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<int>(
            "SELECT `owner_id` FROM `rp_notes` WHERE `id` = @noteId UNION SELECT `user_id` FROM `rp_note_shares` WHERE `note_id` = @noteId", new { noteId }).ToList();
    }

    // ---- summaries (the folders + notes list) ------------------------------

    public static List<FolderRow> GetFolders(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<FolderRow>(
            "SELECT f.`id` AS Id, f.`name` AS Name, " +
            "((SELECT COUNT(*) FROM `rp_notes` n WHERE n.`owner_id` = @userId AND n.`folder_id` = f.`id`) + (SELECT COUNT(*) FROM `rp_note_shares` s WHERE s.`user_id` = @userId AND s.`folder_id` = f.`id`)) AS Count " +
            "FROM `rp_note_folders` f WHERE f.`user_id` = @userId ORDER BY f.`sort_order`, f.`name`", new { userId }).ToList();
    }

    public static List<NoteSummaryRow> GetNotes(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<NoteSummaryRow>(
            "SELECT n.`id` AS Id, n.`owner_id` AS OwnerId, u.`username` AS OwnerName, " +
            "CASE WHEN n.`owner_id` = @userId THEN n.`folder_id` ELSE s.`folder_id` END AS FolderId, " +
            "n.`title` AS Title, SUBSTRING(n.`body`, 1, 160) AS Body, n.`pinned` AS Pinned, n.`updated_at` AS UpdatedAt, " +
            "(SELECT COUNT(*) FROM `rp_note_shares` x WHERE x.`note_id` = n.`id`) AS ShareCount " +
            "FROM `rp_notes` n INNER JOIN `users` u ON u.`id` = n.`owner_id` " +
            "LEFT JOIN `rp_note_shares` s ON s.`note_id` = n.`id` AND s.`user_id` = @userId " +
            "WHERE n.`owner_id` = @userId OR s.`user_id` IS NOT NULL " +
            "ORDER BY n.`pinned` DESC, n.`updated_at` DESC", new { userId }).ToList();
    }

    public static void SendNotes(GameClient session)
    {
        var habbo = session.GetHabbo();
        if (habbo == null) return;
        session.Send(new RpNotesComposer(GetFolders(habbo.Id), GetNotes(habbo.Id)));
    }

    public static void SendNotesTo(IEnumerable<int> userIds)
    {
        foreach (var userId in userIds.Distinct())
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
            if (client?.GetHabbo() != null) SendNotes(client);
        }
    }

    // ---- one note ---------------------------------------------------------

    public static NoteRow GetNote(int noteId, int viewerId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.QueryFirstOrDefault<NoteRow>(
            "SELECT n.`id` AS Id, n.`owner_id` AS OwnerId, u.`username` AS OwnerName, " +
            "CASE WHEN n.`owner_id` = @viewerId THEN n.`folder_id` ELSE s.`folder_id` END AS FolderId, " +
            "n.`title` AS Title, n.`body` AS Body, n.`pinned` AS Pinned, n.`version` AS Version, n.`updated_at` AS UpdatedAt, COALESCE(b.`username`, '') AS UpdatedBy " +
            "FROM `rp_notes` n INNER JOIN `users` u ON u.`id` = n.`owner_id` " +
            "LEFT JOIN `users` b ON b.`id` = n.`updated_by` " +
            "LEFT JOIN `rp_note_shares` s ON s.`note_id` = n.`id` AND s.`user_id` = @viewerId " +
            "WHERE n.`id` = @noteId LIMIT 1", new { noteId, viewerId });
    }

    public static List<PersonRow> GetCollaborators(int noteId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<PersonRow>(
            "SELECT s.`user_id` AS UserId, u.`username` AS Username FROM `rp_note_shares` s INNER JOIN `users` u ON u.`id` = s.`user_id` WHERE s.`note_id` = @noteId ORDER BY u.`username`",
            new { noteId }).ToList();
    }

    public static List<PersonRow> GetFriends(int userId)
    {
        using var connection = PlusEnvironment.DatabaseManager.Connection();
        return connection.Query<PersonRow>(
            "SELECT u.`id` AS UserId, u.`username` AS Username FROM `users` u WHERE u.`id` IN " +
            "(SELECT `user_two_id` FROM `messenger_friendships` WHERE `user_one_id` = @userId UNION SELECT `user_one_id` FROM `messenger_friendships` WHERE `user_two_id` = @userId) " +
            "ORDER BY u.`username` LIMIT 50", new { userId }).ToList();
    }

    public static RpNoteComposer ComposeNote(int noteId, int viewerId)
    {
        var note = GetNote(noteId, viewerId);
        if (note == null) return null;
        var open = Open.TryGetValue(noteId, out var openers) ? openers : null;
        var people = new List<RpNoteComposer.Person>();
        // owner first, then collaborators
        people.Add(new RpNoteComposer.Person(note.OwnerId, note.OwnerName, IsOnline(note.OwnerId), open != null && open.ContainsKey(note.OwnerId), (open != null && open.TryGetValue(note.OwnerId, out var oc)) ? oc : -1));
        foreach (var c in GetCollaborators(noteId))
            people.Add(new RpNoteComposer.Person(c.UserId, c.Username, IsOnline(c.UserId), open != null && open.ContainsKey(c.UserId), (open != null && open.TryGetValue(c.UserId, out var cc)) ? cc : -1));
        var friends = (note.OwnerId == viewerId)
            ? GetFriends(viewerId).Select(f => new RpNoteComposer.Person(f.UserId, f.Username, IsOnline(f.UserId), false, -1)).ToList()
            : new List<RpNoteComposer.Person>();
        return new RpNoteComposer(note, people, friends);
    }

    /// <summary>Push the note to every online collaborator (each composed for themselves).</summary>
    public static void BroadcastNote(int noteId)
    {
        foreach (var userId in CollaboratorIds(noteId))
        {
            var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userId);
            if (client?.GetHabbo() == null) continue;
            var composer = ComposeNote(noteId, userId);
            if (composer != null) client.Send(composer);
        }
    }

    // ---- presence ----------------------------------------------------------

    public static void SetOpen(int noteId, int userId, bool open, int caretLine)
    {
        var openers = Open.GetOrAdd(noteId, _ => new ConcurrentDictionary<int, int>());
        if (open) openers[userId] = caretLine;
        else openers.TryRemove(userId, out _);
        if (openers.IsEmpty) Open.TryRemove(noteId, out _);
    }

    public static void ClearPresence(int userId)
    {
        foreach (var (noteId, openers) in Open.ToList())
        {
            if (openers.TryRemove(userId, out _))
            {
                if (openers.IsEmpty) Open.TryRemove(noteId, out _);
                BroadcastNote(noteId);
            }
        }
    }
}
