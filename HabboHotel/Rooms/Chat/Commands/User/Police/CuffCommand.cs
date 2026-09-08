using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police actions: :cuff - restrain a stunned target.
///
/// Ported from the old Arcturus plugin, minus the gates for being a clocked-in
/// officer and for carrying a pair of handcuffs.
///
/// The one precondition that IS core, and is kept: the target has to be
/// stunned. A player who can walk would simply walk off mid-cuff, which is why
/// the original made :stun the way in. So the police chain reads
/// stun -> cuff -> escort, each step only possible because the last one landed.
///
/// Reach is hands-on: anywhere in the eight tiles around you, or your own,
/// unlike :stun's firing line.
///
/// The stun is the way IN, not something the cuff leans on afterwards: the
/// moment the cuffs land the freeze ends and its visual goes with it, exactly
/// as an escort taking over does. What holds the player from here is the cuff,
/// and a cuff has no clock - it lasts until :uncuff. That is deliberate. A
/// stun expiring used to be what quietly ended the arrest window, so whether
/// an officer got :escort typed in time came down to how fast they type;
/// keying the escort to the cuff alone takes the stopwatch out of it.
///
/// Being cuffed does two things. It stops the cuffed player throwing a punch
/// (see HitCommand and SlapCommand), and it is what :escort needs to take
/// someone into custody - for as long as the cuffs are on, and by any player,
/// not only whoever put them there. It does not stop them walking: a cuffed
/// player is restrained, not rooted, and an officer who wants them moved to a
/// tile of their choosing has :escort for that. It has no visual: the original
/// drew a custom overhead handcuff effect built from its own PNG frames, and
/// neither that bundle nor its sources exist here.
/// </summary>
internal class CuffCommand : ITargetChatCommand
{
    public string Key => "cuff";
    public string PermissionRequired => "command_cuff";

    public string Parameters => "%target%";

    public string Description => "Cuff a stunned user, restraining them.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "No target selected.";

    private const int FightBubble = 4;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        if (target == habbo)
        {
            session.SendWhisper("You cannot cuff yourself.");
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
            session.SendWhisper("You cannot do that while you are being escorted.");
            return Task.CompletedTask;
        }

        // Hands-on reach: the full block around the officer, diagonals in.
        if (Math.Abs(targetUser.X - thisUser.X) > 1 || Math.Abs(targetUser.Y - thisUser.Y) > 1)
        {
            session.SendWhisper($"You need to be right next to {target.Username} to cuff them.");
            return Task.CompletedTask;
        }

        if (!PoliceState.IsStunned(target.Id))
        {
            session.SendWhisper($"{target.Username} has to be stunned before you can cuff them.");
            return Task.CompletedTask;
        }

        if (!PoliceState.Cuff(target.Id))
        {
            session.SendWhisper($"{target.Username} is already cuffed.");
            return Task.CompletedTask;
        }

        // The freeze got the cuffs on; the cuffs hold them now. Ending it here
        // hands walking back and clears the stun's visual, so a cuffed player
        // reads as cuffed rather than as still being tased.
        PoliceState.CancelStun(targetUser);

        room.SendPacket(new ChatComposer(thisUser.VirtualId, $"*cuffs {target.Username}, restraining them*", 0, FightBubble));
        return Task.CompletedTask;
    }
}
