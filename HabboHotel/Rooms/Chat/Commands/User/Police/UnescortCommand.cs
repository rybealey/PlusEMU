using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police actions: :unescort - let the suspect go.
///
/// Takes no target: you can only ever be escorting one person, so the command
/// knows who. Ported alongside :escort because without it a suspect stays
/// pinned until somebody leaves the room.
///
/// The cuffs stay on - this ends the march, not the arrest.
/// </summary>
internal class UnescortCommand : IChatCommand
{
    public string Key => "unescort";
    public string PermissionRequired => "command_unescort";

    public string Parameters => "";

    public string Description => "Stop escorting whoever you have in custody.";

    private const int FightBubble = 4;

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var habbo = session.GetHabbo();
        if (habbo == null || room == null)
            return;

        var suspectId = PoliceState.SuspectOf(habbo.Id);
        if (suspectId == 0)
        {
            session.SendWhisper("You are not escorting anyone.");
            return;
        }

        var suspectUser = room.GetRoomUserManager().GetRoomUserByHabbo(suspectId);
        PoliceState.EndEscort(room, habbo.Id, suspectUser);

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (thisUser == null)
            return;

        var name = suspectUser?.GetClient()?.GetHabbo()?.Username ?? "their suspect";
        room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*releases {name} from custody*", 0, FightBubble));
    }
}
