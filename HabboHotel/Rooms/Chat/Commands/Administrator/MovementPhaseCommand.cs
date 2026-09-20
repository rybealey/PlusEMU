using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Administrator;

/// <summary>
/// pixelrp Movement V2: watch the shared room movement PHASE lifecycle.
/// READ-ONLY - it changes no movement behaviour at all.
///
///   :movementphase on     start watching every room
///   :movementphase off    stop
///   :movementphase        report whether it is armed, and the violation count
///
/// Records go to the EMULATOR CONSOLE LOG tagged `[MV2/phase`, not to chat.
///
/// THE INVARIANT BEING TESTED: the room phase must stay alive while at least
/// one REAL user is Moving or Pending, and a joiner stopping or leaving must
/// not reseed it while another real Moving/Pending avatar remains.
///
/// WHAT INSPECTION ALREADY SETTLES: `PhaseAnchor` has one writer
/// (MovementController.ResolveStartOrigin) and nothing clears it - not
/// RemoveState, not OnUserLeave, not StopWalk, not teardown. So a leaver
/// CLEARING the phase is not possible as such. The reachable failure is a
/// RESEED: ResolveStartOrigin overwrites the anchor whenever HasLivePhase says
/// no other real user is live, so if that ever answers false while one
/// actually is, the room silently splits into two timelines.
///
/// Every Established event is therefore checked against an INDEPENDENT recount
/// rather than trusting the same predicate twice, and a disagreement is logged
/// at WARN as:
///
///   [MV2/phase INVARIANT_VIOLATION]
///
/// That line is the whole point - grep for it first. Everything else is the
/// ordinary lifecycle, at Info:
///
///   ESTABLISHED_FIRST    no phase existed; this walk set one
///   RESEEDED             a phase existed and was overwritten - the suspect
///   REUSED_BY_JOINER     joined an existing phase, waiting startDelayMs
///   REUSED_ON_BOUNDARY   joined at zero cost, already on the boundary
///   SKIPPED_TOO_FAR      boundary further than MaxStartDelayMs, started
///                        unaligned (cannot occur while that ceiling is
///                        IntervalMs, so one of these is itself a finding)
///   BOT_OR_PET_BYPASS    bots and pets neither hold nor follow a phase
///   UNIT_REMOVED         stop / leave / disconnect, with the real
///                        Moving/Pending counts either side of the removal
///                        and phaseCleared, which should never be true
///
/// Bounded at MovementPhaseTrace.MaxLines per armed session; past the cap it
/// stops writing lines but keeps counting violations.
/// </summary>
internal class MovementPhaseCommand : IChatCommand
{
    public string Key => "movementphase";
    public string PermissionRequired => "command_update";
    public string Parameters => "%on/off%";
    public string Description => "Watch the Movement V2 room phase lifecycle (read-only).";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length < 1)
        {
            session.SendWhisper($"Movement phase watch: {MovementPhaseTrace.Stats()}.");
            return;
        }

        switch (parameters[0].ToLowerInvariant())
        {
            case "on":
                MovementPhaseTrace.Arm();
                session.SendWhisper(
                    "Movement phase watch on. Records go to the emulator console log, tagged [MV2/phase] - " +
                    "look for INVARIANT_VIOLATION first.");
                return;

            case "off":
                MovementPhaseTrace.Disarm();
                session.SendWhisper($"Movement phase watch off. {MovementPhaseTrace.Stats()}.");
                return;

            default:
                session.SendWhisper("Usage: :movementphase on, :movementphase off, or :movementphase for status.");
                return;
        }
    }
}
