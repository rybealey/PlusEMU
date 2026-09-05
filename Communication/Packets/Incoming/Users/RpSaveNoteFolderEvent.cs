using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notes;
using Plus.HabboHotel.Rooms.Chat.Filter;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: create (id 0) or rename a folder; an empty name deletes it (its notes drop to no folder).</summary>
internal class RpSaveNoteFolderEvent : IPacketEvent
{
    private readonly IWordFilterManager _wordFilterManager;

    public RpSaveNoteFolderEvent(IWordFilterManager wordFilterManager)
    {
        _wordFilterManager = wordFilterManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var name = _wordFilterManager.CheckMessage(packet.ReadString()).Trim();
        var habbo = session.GetHabbo();
        if (habbo == null) return Task.CompletedTask;
        if (name.Length > NotesUtility.MaxFolderName) name = name.Substring(0, NotesUtility.MaxFolderName);
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (id > 0 && name.Length == 0)
            {
                connection.Execute("UPDATE `rp_notes` SET `folder_id` = NULL WHERE `owner_id` = @userId AND `folder_id` = @id", new { userId = habbo.Id, id });
                connection.Execute("UPDATE `rp_note_shares` SET `folder_id` = NULL WHERE `user_id` = @userId AND `folder_id` = @id", new { userId = habbo.Id, id });
                connection.Execute("DELETE FROM `rp_note_folders` WHERE `id` = @id AND `user_id` = @userId", new { id, userId = habbo.Id });
            }
            else if (id > 0)
                connection.Execute("UPDATE `rp_note_folders` SET `name` = @name WHERE `id` = @id AND `user_id` = @userId", new { id, name, userId = habbo.Id });
            else if (name.Length > 0)
            {
                var count = connection.QueryFirstOrDefault<int>("SELECT COUNT(*) FROM `rp_note_folders` WHERE `user_id` = @userId", new { userId = habbo.Id });
                if (count >= NotesUtility.MaxFolders)
                {
                    session.SendWhisper($"You can have up to {NotesUtility.MaxFolders} folders.");
                    return Task.CompletedTask;
                }
                connection.Execute("INSERT INTO `rp_note_folders` (`user_id`, `name`, `sort_order`, `created_at`) VALUES (@userId, @name, @order, @now)", new { userId = habbo.Id, name, order = count, now = NotesUtility.Now() });
            }
        }
        NotesUtility.SendNotes(session);
        return Task.CompletedTask;
    }
}
