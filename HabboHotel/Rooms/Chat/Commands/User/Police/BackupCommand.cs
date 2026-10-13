using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police: :backup - an on-duty police officer, or on-duty staff (City
/// Government), asks every other on-duty officer to come to the room they are
/// in. It goes into 911 Dispatch
/// as a backup request, and anyone who responds is taken to this room
/// (EmergencyCalls.SubmitBackup). No message, once every 3 minutes. Not while
/// knocked out (CommandManager's KnockedOutCan does not list it).
/// </summary>
internal class BackupCommand : IChatCommand
{
    public string Key => "backup";

    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Call the other on-duty officers to the room you are in.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (session?.GetHabbo() == null || room == null)
            return;
        var reply = EmergencyCalls.SubmitBackup(session);
        if (reply.Length > 0)
            session.SendWhisper(reply);
    }
}
