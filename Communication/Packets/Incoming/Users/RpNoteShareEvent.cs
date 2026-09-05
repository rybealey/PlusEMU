using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notes;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: the owner adds (1) or removes (0) a FRIEND on a note. userId 0 with remove = stop sharing with everyone.</summary>
internal class RpNoteShareEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var userId = packet.ReadInt();
        var add = packet.ReadInt() == 1;
        var habbo = session.GetHabbo();
        if (habbo == null || id <= 0 || !NotesUtility.IsOwner(id, habbo.Id)) return Task.CompletedTask;
        var before = NotesUtility.CollaboratorIds(id);
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (add)
            {
                if (userId <= 0 || userId == habbo.Id || !NotesUtility.AreFriends(habbo.Id, userId))
                {
                    session.SendWhisper("You can only share notes with friends.");
                    return Task.CompletedTask;
                }
                if (before.Count - 1 >= NotesUtility.MaxCollaborators)
                {
                    session.SendWhisper($"A note can be shared with up to {NotesUtility.MaxCollaborators} friends.");
                    return Task.CompletedTask;
                }
                connection.Execute("INSERT IGNORE INTO `rp_note_shares` (`note_id`, `user_id`, `folder_id`, `added_at`) VALUES (@id, @userId, NULL, @now)", new { id, userId, now = NotesUtility.Now() });
            }
            else if (userId > 0)
                connection.Execute("DELETE FROM `rp_note_shares` WHERE `note_id` = @id AND `user_id` = @userId", new { id, userId });
            else
                connection.Execute("DELETE FROM `rp_note_shares` WHERE `note_id` = @id", new { id });
        }
        var after = NotesUtility.CollaboratorIds(id);
        NotesUtility.BroadcastNote(id);
        NotesUtility.SendNotesTo(before.Concat(after));
        return Task.CompletedTask;
    }
}
