using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Gangs;
using Plus.HabboHotel.Groups;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// pixelrp: :leavegang - leave your gang, as the gang window's Leave Gang does
/// (RpGangLeaveEvent). The one difference is the owner: in the window their
/// Leave is Disband, behind a confirm. A typed command gets no confirm, so the
/// owner is told how to hand the gang on or disband it there instead.
/// </summary>
internal class LeaveGangCommand : IChatCommand
{
    private readonly IGroupManager _groupManager;

    public string Key => "leavegang";
    public string PermissionRequired => "";

    public string Parameters => "";

    public string Description => "Leave your gang.";

    public LeaveGangCommand(IGroupManager groupManager)
    {
        _groupManager = groupManager;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        // whispers "You're not in a gang." itself
        var actor = GangManager.GetActor(session, 0);
        if (actor == null)
            return;

        var name = actor.Snapshot.Gang.Name;
        if (actor.IsOwner)
        {
            session.SendWhisper($"You own {name}. Transfer it to another member in the gang window first, or disband it there.");
            return;
        }

        GangManager.RemoveMember(_groupManager, actor.GangId, actor.UserId);
        session.SendWhisper($"You left {name}.");
        GangManager.BroadcastDetail(actor.GangId);
    }
}
