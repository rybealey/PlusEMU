using Plus.HabboHotel.Corporations;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp :escort - move somebody who cannot move themselves.
///
/// ONE command, two jobs, and which one you get is decided by who you work for
/// rather than by what you type. Ported from the old Arcturus plugin, whose
/// :escort had both of these branches from the start.
///
///   CUSTODY   an on-duty police officer marching a cuffed, conscious suspect.
///             The cuffs are the whole basis for it and the last link in the
///             stun -> cuff -> escort chain.
///
///   MEDICAL   an on-duty paramedic carrying a patient who is out cold. The
///             precondition is the exact inverse - no cuffs, and the target
///             MUST be on zero health - and both players wear an ambulance for
///             the trip. The patient is lifted off the floor while carried and
///             laid back down when they are put down.
///
/// Either way, from the moment it starts the passenger does not walk for
/// themselves: the movement engine mirrors every step the escort takes onto
/// them, one tile in front and facing the same way, on the same beat (see
/// MovementV2Bridge.Pair).
///
/// It ends on :unescort, when either of them leaves the room, and for a
/// custody escort on :uncuff. A medical one also ends the moment the patient
/// is healed above zero - the premise is gone.
/// </summary>
internal class EscortCommand : ITargetChatCommand
{
    public string Key => "escort";
    public string PermissionRequired => "command_escort";

    public string Parameters => "%target%";

    public string Description => "Escort a cuffed suspect, or carry an unconscious patient.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "No target selected.";

    private const int FightBubble = 4;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();

        // Which escort this is depends entirely on the caller's job. A player
        // holds exactly one (rp_corporation_employees is keyed by user_id), so
        // there is nothing to disambiguate - and nothing to type differently.
        PoliceState.EscortKind kind;
        if (PoliceUtility.IsOnDutyOfficer(habbo.Id))
            kind = PoliceState.EscortKind.Custody;
        else if (MedicalUtility.IsOnDutyParamedic(habbo.Id))
            kind = PoliceState.EscortKind.Medical;
        else
        {
            // Refuse in the caller's own language. Telling a paramedic that
            // "only police officers can escort someone" is worse than useless:
            // it names a job they do not have and hides the one they do, and an
            // off-duty medic would never learn that clocking in is all they
            // needed. Whichever badge they hold, they hear about that one.
            if (MedicalUtility.IsHospitalStaff(habbo.Id))
                MedicalUtility.RequireOnDutyParamedic(session, "carry a patient");
            else if (PoliceUtility.IsOfficer(habbo.Id))
                PoliceUtility.RequireOnDuty(session, "escort someone");
            else
                session.SendWhisper("Only police officers and paramedics can escort someone.");
            return Task.CompletedTask;
        }

        // pixelrp: never on one of your own characters - see ChargeCommand.
        if (AccountUtility.SameAccount(habbo.Id, target.Id))
        {
            session.SendWhisper("That is one of your own characters.");
            return Task.CompletedTask;
        }
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

        // The two branches want opposite things of the target, which is the
        // whole point: custody moves someone restrained, medical moves someone
        // who cannot move at all.
        target.EnsureRpStatsLoaded();
        if (kind == PoliceState.EscortKind.Custody)
        {
            if (!PoliceState.IsCuffed(target.Id))
            {
                session.SendWhisper($"{target.Username} has to be cuffed before you can escort them.");
                return Task.CompletedTask;
            }

            // A custody escort ends the moment its suspect is knocked out, so
            // it cannot begin on one either - nobody marches lying down.
            if (target.RpHealth <= 0)
            {
                session.SendWhisper($"{target.Username} is out cold and cannot be escorted.");
                return Task.CompletedTask;
            }
        }
        else if (target.RpHealth > 0)
        {
            // No cuffs are asked for here and none would help. A patient who is
            // on their feet can walk to the hospital themselves.
            session.SendWhisper($"{target.Username} is conscious and does not need to be carried.");
            return Task.CompletedTask;
        }

        // Hands-on, like the cuff itself - and like lifting somebody.
        if (Math.Abs(targetUser.X - thisUser.X) > 1 || Math.Abs(targetUser.Y - thisUser.Y) > 1)
        {
            session.SendWhisper(kind == PoliceState.EscortKind.Medical
                ? $"You need to be right next to {target.Username} to lift them."
                : $"You need to be right next to {target.Username} to escort them.");
            return Task.CompletedTask;
        }

        if (!PoliceState.StartEscort(room, thisUser, targetUser, kind))
        {
            session.SendWhisper($"{target.Username} is already being escorted.");
            return Task.CompletedTask;
        }

        // The freeze has done its job; the escort takes over holding them.
        PoliceState.CancelStun(targetUser);

        room.SendPacket(new ChatComposer(thisUser.VirtualId, kind == PoliceState.EscortKind.Medical
            ? $"*loads {target.Username} into an ambulance*"
            : $"*takes {target.Username} into custody*", 0, FightBubble));
        return Task.CompletedTask;
    }
}
