using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Rooms.Gym;

namespace Plus.HabboHotel.Users;

/// <summary>
/// pixelrp: the vape (Twist, 2026-10-20). It is in the hand (hand item 1129,
/// docker/nitro/make-vape-handitem.py) only while it is EQUIPPED in the Weapon
/// slot (Twist, 2026-10-22) - held like a weapon (RpWeapons.IsEquippable),
/// under the open phone or anything carried. Clicked in the backpack it is
/// equipped; the equipped one, clicked, takes a hit: "*takes a hit from their
/// vape*" in the blue action bubble, the
/// vape goes to their mouth and they breathe out a puff of smoke (effect 640,
/// docker/nitro/make-vape-smoke.py; the client raises the vape for the puff's
/// first second, which draws nothing, and the smoke comes as it goes down).
/// Only staff hand them out for now (:spawn vape).
///
/// A vape holds seven hits, counted like the stun gun's seven shots: per
/// player and in memory - a relog keeps the count, an emulator restart fills
/// it again - and drawn as the green bar on the vape in the backpack
/// (RpVapeChargeComposer). The seventh hit uses the vape up: one goes from the
/// slot and the count starts again for the next. A vape coming into a backpack
/// that holds none starts full (RpInventoryStore.Add, and trades) - but not
/// one coming back to the player it left, out of their deposit box or a
/// Marketplace listing taken back: that one keeps their hits (fillVape).
///
/// The puff borrows the effect slot the way the police beacon does: whatever
/// was worn comes back when it ends, if the puff is still what is worn.
/// </summary>
public static class RpVape
{
    /// <summary>The backpack item key.</summary>
    public const string Item = "vape";

    /// <summary>Hits in one vape.</summary>
    public const int Hits = 7;

    public const int EffectId = 640;

    /// <summary>
    /// The vape in the hand. Over 999, so the client only carries it and never
    /// raises it on its own the way it sips a drink; a hit raises it.
    /// </summary>
    public const int HandItem = 1129;

    /// <summary>The blue action bubble the snack and the medkit use.</summary>
    private const int ActionBubble = 4;

    /// <summary>
    /// How long the puff is worn: the second the vape is at the mouth, then the
    /// smoke for about 2.2 s. After that the animation draws nothing, so a
    /// little late is never a second puff.
    /// </summary>
    private const int PuffMs = 3600;

    /// <summary>
    /// A hand busy with something else lends the vape the hand for the hit, as
    /// a drink would take it: room cycles (0.5 s) before it goes back.
    /// </summary>
    private const int BorrowCycles = 8;

    /// <summary>One hit at a time: the next waits until this puff has gone.</summary>
    private const int CooldownSeconds = 4;

    /// <summary>
    /// Effects other systems put on and take off again - the stun's birds (and
    /// pepper spray's), cuffs, the escort's ambulance, the gym machines, the
    /// bin search's flies and the police beacon. Each takes its own off only
    /// while it is still worn, so a puff over one would hand it back for good:
    /// no hit while one is on.
    /// </summary>
    private static readonly HashSet<int> BusyEffects = new()
    {
        PoliceState.StunEffectId, PoliceState.CuffEffectId, PoliceState.AmbulanceEffectId,
        GymState.TreadmillEffectId, GymState.CrossTrainerEffectId, BinSearch.FliesEffectId, Corporations.PoliceBeacon.EffectId
    };

    /// <summary>Hits each player has taken from their current vape. No entry reads as a full one.</summary>
    private static readonly ConcurrentDictionary<int, int> HitsTaken = new();

    private static readonly ConcurrentDictionary<int, DateTime> LastHit = new();

    private sealed class Puffing
    {
        public Puffing(Habbo habbo, int before)
        {
            Habbo = habbo;
            Before = before;
        }

        /// <summary>The session it started in - a relog is a new one.</summary>
        public Habbo Habbo { get; }
        /// <summary>The effect worn underneath, given back when the puff ends.</summary>
        public int Before { get; }
    }

    /// <summary>Puffs showing, by user id. Replaced rather than changed, so a timer only ends its own.</summary>
    private static readonly ConcurrentDictionary<int, Puffing> Puffs = new();

    public static bool IsPuffing(int habboId) => Puffs.ContainsKey(habboId);

    public static int HitsLeft(int habboId) => Math.Max(0, Hits - HitsTaken.GetValueOrDefault(habboId));

    /// <summary>
    /// Bring the hand in line with the Weapon slot (RpWeapons.ApplyToHand) -
    /// an equipped vape in it, a vape in the hand. Left alone mid-puff, so a
    /// hit that uses the last one up keeps it at the mouth until the puff ends,
    /// which syncs it then. A player not in a room has no hand; room entry
    /// catches them up.
    /// </summary>
    public static void SyncHand(Habbo habbo, List<(int Slot, string Item, int Count)> inventory = null)
    {
        if (habbo == null || IsPuffing(habbo.Id))
            return;
        RpWeapons.ApplyToHand(habbo, inventory);
    }

    /// <summary>A vape left this player's backpack (RpInventoryStore) - the equipped one, perhaps: their hand follows, if they are on.</summary>
    public static void SyncHand(int habboId) =>
        SyncHand(PlusEnvironment.Game.ClientManager.GetClientByUserId(habboId)?.GetHabbo());

    /// <summary>A full vape again - one has come into a backpack that held none.</summary>
    public static void Refill(int habboId)
    {
        if (!HitsTaken.TryRemove(habboId, out _))
            return;
        PlusEnvironment.Game.ClientManager.GetClientByUserId(habboId)?.Send(new RpVapeChargeComposer(Hits, Hits));
    }

    /// <summary>
    /// A trade changed the backpack (RpTrades): a vape received into a
    /// backpack that held none starts full, and the hand follows the Weapon
    /// slot, in case the equipped one was given away.
    /// </summary>
    public static void Traded(Habbo habbo, List<(int Slot, string Item, int Count)> inventory, int given, int received)
    {
        var now = inventory.Where(entry => entry.Item == Item).Sum(entry => entry.Count);
        if (received > 0 && now - received + given == 0)
            Refill(habbo.Id);
        SyncHand(habbo, inventory);
    }

    /// <summary>The equipped vape (in the Weapon slot, <paramref name="slot"/>) was clicked (RpUseItemEvent).</summary>
    public static void TakeHit(GameClient session, int slot)
    {
        var habbo = session?.GetHabbo();
        var user = habbo?.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (user == null || habbo.Effects == null)
            return;
        if (LastHit.TryGetValue(habbo.Id, out var last))
        {
            var elapsed = (DateTime.UtcNow - last).TotalSeconds;
            if (elapsed < CooldownSeconds)
            {
                var remaining = (int)Math.Ceiling(CooldownSeconds - elapsed);
                session.SendWhisper($"Cooldown [{remaining}/{CooldownSeconds}]");
                return;
            }
        }
        if (BusyEffects.Contains(habbo.Effects.CurrentEffect))
        {
            session.SendWhisper("You can't use your vape right now.");
            return;
        }
        // The vape goes to the mouth from the hand. The open phone or a drink
        // lend it the hand for the hit; nothing goes into a hand holding a
        // stun gun (RoomUser.StunGunDrawn).
        if (user.CarryItemId != HandItem)
        {
            if (!user.CanCarry(HandItem))
            {
                session.SendWhisper("Put your stun gun away first.");
                return;
            }
            user.CarryItem(HandItem);
            user.CarryTimer = BorrowCycles;
        }
        LastHit[habbo.Id] = DateTime.UtcNow;
        Plus.Utilities.StaleEntries.Sweep(LastHit, (_, at) => (DateTime.UtcNow - at).TotalSeconds >= CooldownSeconds);

        user.OnChat(ActionBubble, "*takes a hit from their vape*", true);
        Puff(habbo);
        if (HitsTaken.AddOrUpdate(habbo.Id, 1, (_, taken) => taken + 1) >= Hits)
        {
            HitsTaken.TryRemove(habbo.Id, out _);
            habbo.ConsumeRpItem(slot);
            session.Send(new RpInventoryComposer(habbo.LoadRpInventory()));
        }
        session.Send(new RpVapeChargeComposer(HitsLeft(habbo.Id), Hits));
    }

    private static void Puff(Habbo habbo)
    {
        var before = habbo.Effects.CurrentEffect;
        if (before == EffectId)
            before = Puffs.TryGetValue(habbo.Id, out var showing) ? showing.Before : 0;
        var puff = new Puffing(habbo, before);
        Puffs[habbo.Id] = puff;
        habbo.Effects.ApplyEffect(EffectId);
        _ = Task.Run(async () =>
        {
            await Task.Delay(PuffMs);
            try
            {
                if (Puffs.TryRemove(new KeyValuePair<int, Puffing>(habbo.Id, puff)))
                {
                    Restore(puff);
                    SyncHand(habbo);
                }
            }
            catch (Exception exception)
            {
                Plus.Core.ExceptionLogger.LogException(exception);
            }
        });
    }

    private static void Restore(Puffing puff)
    {
        var habbo = puff.Habbo;
        var effects = habbo.Effects;
        if (habbo.Client?.GetHabbo() != habbo || effects == null || effects.CurrentEffect != EffectId)
            return;
        // ApplyEffect needs a room to tell; between rooms the next room's
        // entry shows whatever is current.
        if (habbo.CurrentRoom?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id) != null)
            effects.ApplyEffect(puff.Before);
        else
            effects.CurrentEffect = puff.Before;
    }
}
