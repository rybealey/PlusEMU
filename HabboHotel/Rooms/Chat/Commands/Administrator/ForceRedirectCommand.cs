using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Administrator;

/// <summary>
/// pixelrp Movement V2: force the near-boundary redirect that causes the hitch.
///
/// NOT READ-ONLY, and the only movement command that is not. The other four
/// observe; this one moves your avatar. It issues a redirect you did not click,
/// a few milliseconds before an edge boundary, which is the window the hitch
/// lives in and the one a human cannot hit on purpose.
///
///   :forceredirect on                 arm on yourself, 5ms margin, 20 runs
///   :forceredirect on &lt;margin&gt;        arm with a different margin, in ms
///   :forceredirect on &lt;margin&gt; &lt;runs&gt; and a different run count
///   :forceredirect status             attempts so far, and the verdict tally
///   :forceredirect off                stop now
///
/// IT ARMS ON YOU, never on somebody else. The harness waits for its subject to
/// be a JOINER - a walker that fell in behind a phase another player was
/// already holding - and then walks them somewhere they did not ask to go.
/// Doing that to a player who did not type the command is not a diagnostic, it
/// is a glitch with an author.
///
/// HOW TO USE IT. Get a second player walking in the room, start walking
/// yourself so you join their phase, and the harness fires on its own from
/// there. It disarms itself after the run count.
///
/// READ IT IN THE BROWSER. Open the console (F12) and every forced redirect
/// prints [MV2/FORCED] on its own - no switch to arm, no VPS access - carrying
/// the margin the server achieved beside the client's own phaseNow, which says
/// whether that edge was already being drawn when the rewrite landed. The same
/// runs are also logged emulator-side as [MV2/force] for anyone on the host.
/// </summary>
internal class ForceRedirectCommand : IChatCommand
{
    public string Key => "forceredirect";
    public string PermissionRequired => "command_update";
    public string Parameters => "%on/off/status% %margin% %runs%";
    public string Description => "Force a near-boundary redirect on yourself, to reproduce the movement hitch.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length < 1)
        {
            session.SendWhisper(
                "Usage: :forceredirect on, :forceredirect on <margin> <runs>, :forceredirect status, or :forceredirect off.");
            return;
        }

        switch (parameters[0].ToLowerInvariant())
        {
            case "off":
                MovementForceRedirect.Disarm("command");
                session.SendWhisper("Force redirect off.");
                return;

            case "status":
                session.SendWhisper(MovementForceRedirect.Enabled
                    ? $"Force redirect is armed. {MovementForceRedirect.Stats()}."
                    : $"Force redirect is not armed. Last run: {MovementForceRedirect.Stats()}.");
                return;

            case "on":
                break;

            default:
                session.SendWhisper(
                    "Usage: :forceredirect on, :forceredirect on <margin> <runs>, :forceredirect status, or :forceredirect off.");
                return;
        }

        if (room == null)
        {
            session.SendWhisper("You need to be in a room to arm this.");
            return;
        }

        var user = room.GetRoomUserManager()?.GetRoomUserByHabbo(session.GetHabbo().Id);
        if (user == null)
        {
            session.SendWhisper("You are not in this room yet - try again in a moment.");
            return;
        }

        if (!MovementV2Bridge.Owns(user))
        {
            session.SendWhisper("Movement V2 does not own your avatar, so there is nothing to redirect.");
            return;
        }

        var margin = MovementForceRedirect.DefaultMarginMs;
        if (parameters.Length > 1 && !int.TryParse(parameters[1], out margin))
        {
            session.SendWhisper("The margin has to be a whole number of milliseconds.");
            return;
        }

        var runs = MovementForceRedirect.DefaultRuns;
        if (parameters.Length > 2 && !int.TryParse(parameters[2], out runs))
        {
            session.SendWhisper("The run count has to be a whole number.");
            return;
        }

        if (margin < 0 || margin > MovementForceRedirect.MaxMarginMs)
        {
            session.SendWhisper(
                $"The margin has to be between 0 and {MovementForceRedirect.MaxMarginMs}ms.");
            return;
        }

        if (runs < 1 || runs > MovementForceRedirect.MaxRuns)
        {
            session.SendWhisper($"The run count has to be between 1 and {MovementForceRedirect.MaxRuns}.");
            return;
        }

        MovementForceRedirect.Arm(user.RoomId, user.VirtualId, user.GetUsername(), margin, runs);

        session.SendWhisper(
            $"Force redirect armed on you at {margin}ms before the boundary, for {runs} runs. " +
            "It fires once you are walking and have joined somebody else's phase, and disarms itself at the end. " +
            "Records go to the emulator console log, tagged [MV2/force].");
    }
}
