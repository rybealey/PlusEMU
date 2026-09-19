using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Movement;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Administrator;

/// <summary>
/// pixelrp Movement V2: arm the route-revision capture. READ-ONLY - it changes
/// no movement behaviour at all.
///
///   :movementreplan on               capture every unit in the hotel
///   :movementreplan &lt;username&gt;       capture just that avatar
///   :movementreplan flush            emit anything still held, keep capturing
///   :movementreplan off              emit what is held, then stop
///
/// THE RECORDS GO TO THE EMULATOR CONSOLE LOG, not to chat - this command only
/// arms and disarms, and whispers a one-line acknowledgement. Read the lines
/// with `docker logs` on the host; they are tagged `[MV2/replan`.
///
/// A line is written when the revised edge's 4110 goes out, so it can carry the
/// real send timestamp - that is the whole discriminator. A revision whose index
/// never goes out is swept after MovementReplanTrace.StaleMs and logged NOSEND,
/// so nothing is silently lost.
///
/// READ THE VERDICT FIRST. It answers the one question this exists for:
///
///   A_ACTIVE  the server restaged an index at or before the elapsing edge.
///             Decisive: a genuine server bug, and a server fix.
///   B_LATE    the index was future server-side, but the 4110 left AFTER that
///             edge's own cycleStart, so the client had already begun it from
///             lookahead. Decisive for B - and unfixable on the server, since
///             no packet can arrive before it was sent.
///   B_RISK    same, but the packet left within FlightAllowanceMs of the start.
///             SUGGESTIVE ONLY: the server cannot see receipt time.
///   CLEAN     restaged with real margin. Not the hitch.
///   SAME      the revision did not actually change that index's geometry.
///   NOSEND    the index never went out under this revision (superseded).
///
/// A run of A_ACTIVE says fix the server. A run of B_LATE with no A_ACTIVE says
/// the server was never wrong and the client is the only place left to stand.
///
/// THE THREE TIMESTAMPS, which are three different moments and routinely get
/// conflated. For the replaced index:
///
///   serverNow  -&gt; stagedAt  -&gt; sentAt      against  startTick
///   planMargin    stageMargin  sendMargin   = startTick minus each
///   revToStage    stageToSend               = the gaps between them
///
/// A NEGATIVE MARGIN MEANS THAT MOMENT WAS ALREADY PAST THE EDGE'S OWN START.
/// Read them together: a healthy planMargin with a negative stageMargin says
/// the revision was decided in good time and the RECORD WAS NOT BUILT UNTIL THE
/// BOUNDARY - which would make the race structural rather than incidental,
/// because StageCorrection stages nothing and the next beat is queued at
/// exactly e+1's cycleStart.
///
///   activeEdge / phaseInE        what the server believed when it PLANNED
///   elapsingAtSend               what had become true by the time it SHIPPED
///   alreadyActiveWhenSent        yes = the client had certainly begun it
///
/// Those last two are deliberately NOT called the same thing as activeEdge.
/// One name spanning two causes is how this investigation lost months.
/// </summary>
internal class MovementReplanCommand : IChatCommand
{
    public string Key => "movementreplan";
    public string PermissionRequired => "command_update";
    public string Parameters => "%on/off/flush/username%";
    public string Description => "Log Movement V2 route revisions near the active edge (read-only).";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length < 1)
        {
            session.SendWhisper(
                "Usage: :movementreplan on, :movementreplan <username>, :movementreplan flush, or :movementreplan off.");
            return;
        }

        switch (parameters[0].ToLowerInvariant())
        {
            case "off":
                MovementReplanTrace.Disarm();
                session.SendWhisper("Movement replan capture off - anything held has been written to the console log.");
                return;

            case "on":
                MovementReplanTrace.Arm(-1);
                session.SendWhisper(
                    "Movement replan capture on for every unit. Records go to the emulator console log, tagged [MV2/replan].");
                return;

            case "flush":
                if (!MovementReplanTrace.Enabled)
                {
                    session.SendWhisper("Movement replan capture is not armed - :movementreplan on first.");
                    return;
                }
                MovementReplanTrace.Flush();
                session.SendWhisper($"Movement replan capture flushed to the console log. {MovementReplanTrace.Stats()}.");
                return;
        }

        if (room == null)
        {
            session.SendWhisper("You need to be in a room to capture a single avatar.");
            return;
        }

        var target = room.GetRoomUserManager()?.GetRoomUserByHabbo(parameters[0]);
        if (target == null)
        {
            session.SendWhisper($"{parameters[0]} is not in this room.");
            return;
        }

        MovementReplanTrace.Arm(target.VirtualId);
        session.SendWhisper(
            $"Movement replan capture on for {target.GetUsername()} (unit {target.VirtualId}). " +
            "Records go to the emulator console log, tagged [MV2/replan].");
    }
}
