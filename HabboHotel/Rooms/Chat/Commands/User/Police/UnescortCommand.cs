using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp :unescort - put down whoever you are moving.
///
/// Takes no target: you can only ever be escorting one person, so the command
/// knows who. Ported alongside :escort because without it a suspect stays
/// pinned until somebody leaves the room.
///
/// Ends either flavour, and says so in the right words. The cuffs stay on -
/// this ends the march, not the arrest - and a patient put down still out cold
/// is laid back on the floor by EndEscort.
/// </summary>
internal class UnescortCommand : IChatCommand
{
    public string Key => "unescort";
    public string PermissionRequired => "command_unescort";

    public string Parameters => "";

    public string Description => "Stop escorting whoever you are moving.";

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
        // Asked BEFORE the escort ends, because ending it is what forgets which
        // kind it was.
        var medical = PoliceState.IsMedicalEscort(habbo.Id);
        PoliceState.EndEscort(room, habbo.Id, suspectUser);

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (thisUser == null)
            return;

        var name = suspectUser?.GetClient()?.GetHabbo()?.Username ?? (medical ? "their patient" : "their suspect");
        room.SendPacket(new ChatComposer(thisUser.VirtualId, medical
            ? $"*unloads {name} from the ambulance*"
            : $"*releases {name} from custody*", 0, FightBubble));
    }
}
