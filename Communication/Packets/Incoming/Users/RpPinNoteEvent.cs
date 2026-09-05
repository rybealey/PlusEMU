using Dapper;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Notes;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: pin (1) or unpin (0) a note - shared across everyone in it.</summary>
internal class RpPinNoteEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadInt();
        var pinned = packet.ReadInt() == 1;
        var habbo = session.GetHabbo();
        if (habbo == null || id <= 0 || !NotesUtility.CanAccess(id, habbo.Id)) return Task.CompletedTask;
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
            connection.Execute("UPDATE `rp_notes` SET `pinned` = @pinned WHERE `id` = @id", new { id, pinned = pinned ? 1 : 0 });
        NotesUtility.SendNotesTo(NotesUtility.CollaboratorIds(id));
        return Task.CompletedTask;
    }
}
