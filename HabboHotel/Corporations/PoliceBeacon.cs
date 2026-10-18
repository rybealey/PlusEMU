using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: the blue police beacon (effect 639, built by
/// docker/nitro/make-police-beacon.py) over an on-duty officer on their way to
/// a 911 call or a backup request (Twist, 2026-10-19; design: the "Police
/// siren duty badge" canvas).
///
/// It lights when Respond or Go to room starts the trip
/// (EmergencyCalls.StartTrip), rides with them into the caller's room, and goes
/// out three seconds after they walk in. Whatever calls the trip off puts it
/// out at once: walking away, being hit, stunned, cuffed and the rest, a
/// knockout, leaving the room some other way, clocking off. On-duty police
/// only - staff answering a call keep the staff enable (102), and from the
/// hotel view there is no wait, so nothing to light.
///
/// When it goes out, whatever they wore before comes back - if the beacon is
/// still what they wear (the cuffs' rule: something that has taken the slot
/// since keeps it). Nothing is persisted.
/// </summary>
public static class PoliceBeacon
{
    public const int EffectId = 639;

    /// <summary>How long it stays on once the officer has walked into the caller's room.</summary>
    private const int AfterArrivalMs = 3000;

    /// <summary>
    /// The arrival should show up as a room entry within a few seconds; if it
    /// never does (a door that turned them away, a lost connection), it goes
    /// out anyway.
    /// </summary>
    private const int ArrivalTimeoutMs = 15000;

    private sealed class Light
    {
        public Light(Habbo habbo, int before, bool arriving)
        {
            Habbo = habbo;
            Before = before;
            Arriving = arriving;
        }

        /// <summary>The session it was lit in - a relog is a new one.</summary>
        public Habbo Habbo { get; }
        /// <summary>The effect worn underneath, given back when it goes out.</summary>
        public int Before { get; }
        /// <summary>The trip went: the next room entry is the caller's room.</summary>
        public bool Arriving { get; }
    }

    /// <summary>
    /// Lit beacons, by user id. A light is replaced rather than changed, so a
    /// timer only ever puts out the light it was started for.
    /// </summary>
    private static readonly ConcurrentDictionary<int, Light> Lights = new();

    public static bool IsLit(int habboId) => Lights.ContainsKey(habboId);

    /// <summary>The trip has started (EmergencyCalls.StartTrip): an on-duty officer lights up.</summary>
    public static void LightUp(Habbo habbo)
    {
        if (habbo?.Effects == null || ShiftManager.IsStaffOnDuty(habbo.Id) || !PoliceUtility.IsOnDutyOfficer(habbo.Id))
            return;
        var before = habbo.Effects.CurrentEffect;
        if (before == EffectId)
            before = Lights.TryGetValue(habbo.Id, out var lit) ? lit.Before : 0;
        Lights[habbo.Id] = new Light(habbo, before, arriving: false);
        habbo.Effects.ApplyEffect(EffectId);
    }

    /// <summary>
    /// The trip went (EmergencyCalls.Arrive): the beacon stays on into the
    /// caller's room, where <see cref="EnteredRoom"/> starts its last seconds.
    /// </summary>
    public static void Arriving(Habbo habbo)
    {
        if (habbo == null || !Lights.TryGetValue(habbo.Id, out var lit) || lit.Habbo != habbo)
            return;
        var going = new Light(habbo, lit.Before, arriving: true);
        if (Lights.TryUpdate(habbo.Id, going, lit))
            PutOutLater(habbo.Id, going, ArrivalTimeoutMs);
    }

    /// <summary>
    /// Room entry (RoomUserManager): an arriving officer walks in with the
    /// beacon on, and the room is shown it - the effect lives on the Habbo, so
    /// it came along, but nobody here has seen it. True when it was shown.
    /// Lit but not arriving means they left mid-trip some other way (the
    /// navigator, a teleporter): the trip is off, and so is the beacon.
    /// </summary>
    public static bool EnteredRoom(Habbo habbo, Room room, RoomUser user)
    {
        if (habbo == null || !Lights.TryGetValue(habbo.Id, out var lit) || lit.Habbo != habbo)
            return false;
        if (!lit.Arriving || habbo.Effects?.CurrentEffect != EffectId)
        {
            PutOut(habbo.Id);
            return false;
        }
        var staying = new Light(habbo, lit.Before, arriving: false);
        if (!Lights.TryUpdate(habbo.Id, staying, lit))
            return false;
        room.SendPacket(new AvatarEffectComposer(user.VirtualId, EffectId));
        PutOutLater(habbo.Id, staying, AfterArrivalMs);
        return true;
    }

    /// <summary>The trip is off, or the shift is over: out now.</summary>
    public static void PutOut(int habboId)
    {
        if (Lights.TryRemove(habboId, out var lit))
            Restore(lit);
    }

    private static void PutOutLater(int habboId, Light lit, int delayMs)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            try
            {
                if (Lights.TryRemove(new KeyValuePair<int, Light>(habboId, lit)))
                    Restore(lit);
            }
            catch (Exception exception)
            {
                Plus.Core.ExceptionLogger.LogException(exception);
            }
        });
    }

    private static void Restore(Light lit)
    {
        var habbo = lit.Habbo;
        var effects = habbo.Effects;
        if (habbo.Client?.GetHabbo() != habbo || effects == null || effects.CurrentEffect != EffectId)
            return;
        // ApplyEffect needs a room to tell; between rooms the next room's
        // entry shows whatever is current.
        if (habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id) != null)
            effects.ApplyEffect(lit.Before);
        else
            effects.CurrentEffect = lit.Before;
    }
}
