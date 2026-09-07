using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police actions: :uncuff - let a cuffed player go.
///
/// The original had this alongside :cuff and it is not optional here either:
/// without it a cuff only ends when someone leaves the room, which would make
/// the pair untestable and would leave a player unable to fight with no way
/// back. Anyone may use it for now, like the rest of the chain.
///
/// Uncuffing also ends any escort the player is in. An escort exists to move
/// someone who is restrained; once the cuffs are off there is nothing holding
/// them, and leaving the escort running would pin a free player to a captor.
/// </summary>
internal class UncuffCommand : ITargetChatCommand
{
    public string Key => "uncuff";
    public string PermissionRequired => "command_uncuff";

    public string Parameters => "%target%";

    public string Description => "Take the cuffs off a restrained user.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "No target selected.";

    private const int FightBubble = 4;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser == null)
        {
            session.SendWhisper($"{target.Username} is not in this room.");
            return Task.CompletedTask;
        }

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (thisUser == null)
            return Task.CompletedTask;

        if (!PoliceState.Uncuff(target.Id))
        {
            session.SendWhisper($"{target.Username} is not cuffed.");
            return Task.CompletedTask;
        }

        // The cuffs were what justified the escort.
        var captorId = PoliceState.CaptorOf(target.Id);
        if (captorId != 0)
            PoliceState.EndEscort(room, captorId, targetUser);

        room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*unlocks {target.Username}'s cuffs*", 0, FightBubble));
        return Task.CompletedTask;
    }
}
