using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police actions: :escort - march a cuffed suspect around.
///
/// Ported from the old Arcturus plugin's police-escort flow (its :escort also
/// had a paramedic branch, which is not part of this). The clocked-in-officer
/// gate is not here yet.
///
/// The suspect has to be cuffed - that is the whole basis for the escort, and
/// the last link in the stun -> cuff -> escort chain. From the moment it
/// starts they cannot walk for themselves: every tile they cover is one the
/// captor's own steps give them, shoved along one tile in front (see
/// PoliceState.DragSuspect). Starting an escort cancels any stun still
/// running on them, so the drag can move them at once rather than waiting out
/// a freeze.
///
/// It ends on :unescort, on :uncuff, or when either of them leaves the room.
/// </summary>
internal class EscortCommand : ITargetChatCommand
{
    public string Key => "escort";
    public string PermissionRequired => "command_escort";

    public string Parameters => "%target%";

    public string Description => "Escort a cuffed user, walking them with you.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "No target selected.";

    private const int FightBubble = 4;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        if (target == habbo)
        {
            session.SendWhisper("You cannot escort yourself.");
            return Task.CompletedTask;
        }

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser == null)
        {
            session.SendWhisper($"{target.Username} is not in this room.");
            return Task.CompletedTask;
        }

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (thisUser == null)
            return Task.CompletedTask;

        if (PoliceState.IsBeingEscorted(habbo.Id))
        {
            session.SendWhisper("You cannot escort anyone while you are being escorted.");
            return Task.CompletedTask;
        }

        if (PoliceState.IsEscorting(habbo.Id))
        {
            var current = PoliceState.SuspectOf(habbo.Id);
            session.SendWhisper(current == target.Id
                ? $"You are already escorting {target.Username}."
                : "You are already escorting someone. Use :unescort first.");
            return Task.CompletedTask;
        }

        if (!PoliceState.IsCuffed(target.Id))
        {
            session.SendWhisper($"{target.Username} has to be cuffed before you can escort them.");
            return Task.CompletedTask;
        }

        // Hands-on, like the cuff itself.
        if (Math.Abs(targetUser.X - thisUser.X) > 1 || Math.Abs(targetUser.Y - thisUser.Y) > 1)
        {
            session.SendWhisper($"You need to be right next to {target.Username} to escort them.");
            return Task.CompletedTask;
        }

        if (!PoliceState.StartEscort(habbo.Id, target.Id, targetUser))
        {
            session.SendWhisper($"{target.Username} is already being escorted.");
            return Task.CompletedTask;
        }

        // The freeze has done its job; the escort takes over holding them.
        PoliceState.CancelStun(targetUser);

        room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*takes {target.Username} into custody*", 0, FightBubble));
        return Task.CompletedTask;
    }
}
