using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Fun;

/// <summary>
/// pixelrp: spit at another player.
///
/// It ALWAYS lands somewhere. In range it hits the target and a splat appears
/// on their tile; out of range it dribbles down the spitter and the splat
/// appears on theirs. There is no refusal branch and no "too far away"
/// whisper - missing is part of the joke, and a command that answers a bad
/// aim with a silent error is not funny, it is broken-feeling.
///
/// Reach is :stun's firing line, exactly: two tiles straight along a row or
/// column, one tile on a true diagonal, and nothing off those lines - a
/// knight's-move target is out of reach however close it looks. Spitting
/// carries like a shot does, not like a fist.
///
/// The splat is a GHOST. It is built in memory and sent straight to the room,
/// never written to `items`, and taken away again a few seconds later. Nobody
/// owns it, it survives no reload, and it cannot be picked up - which is what
/// we want from a few seconds of mess, and which keeps a social gag from
/// writing a row per use into a table that is never swept.
///
/// Damage matches :slap exactly - 1 point, unsafe zones only, same passive,
/// cuff and escort rules - because the two are the same weight of act. In a
/// safe zone it stays pure flavour: the bubble goes out, the splat appears,
/// nobody loses health and nobody becomes aggressive.
/// </summary>
internal class SpitCommand : ITargetChatCommand
{
    public string Key => "spit";
    public string PermissionRequired => "command_spit";

    public string Parameters => "%target%";

    public string Description => "Spit at another user.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "No target selected.";

    /// <summary>
    /// Blue bubble, the same one :hit and :slap use. Spitting at somebody is a
    /// fighting word even where it costs no health, and it belongs with them
    /// rather than with the white star bubble the staff RP commands wear.
    /// </summary>
    private const int FightBubble = 4;

    /// <summary>Seconds between spits. :slap's five - the same weight of act.</summary>
    private const int CooldownSeconds = 5;

    /// <summary>Health a spit takes off, in an unsafe zone.</summary>
    private const int Damage = 1;

    /// <summary>What a spit that lands sets the spitter's aggression to.</summary>
    private const int AggressionOnSpit = 100;

    /// <summary>How far a spit carries along a row or column. :stun's reach.</summary>
    private const int StraightReach = 2;

    /// <summary>How long the splat stays on the floor.</summary>
    private static readonly TimeSpan SplatLifetime = TimeSpan.FromSeconds(4);

    /// <summary>The furni that gets thrown: "Blue Paint Splat".</summary>
    private const string SplatItemName = "xmas13_paintsplat2";

    /// <summary>
    /// Ids for the ghost splats, counting DOWN from the top of the range.
    /// Real furni ids come from an auto-increment climbing from 1, so nothing
    /// the hotel owns will ever reach here and the client cannot confuse a
    /// splat with a real item it already knows about.
    /// </summary>
    private static uint _ghostId = uint.MaxValue;

    private readonly ConcurrentDictionary<int, DateTime> _lastSpit = new();

    /// <summary>
    /// Injected rather than reached through PlusEnvironment.Game.ItemManager,
    /// which is marked obsolete for exactly this. Commands are resolved from
    /// the container, so a constructor dependency is the house pattern.
    /// </summary>
    private readonly IItemDataManager _itemDataManager;

    public SpitCommand(IItemDataManager itemDataManager)
    {
        _itemDataManager = itemDataManager;
    }

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (target == session.GetHabbo())
        {
            session.SendWhisper("You cannot spit at yourself.");
            return Task.CompletedTask;
        }

        var targetUser = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);
        if (targetUser == null)
        {
            session.SendWhisper($"{target.Username} is not in this room.");
            return Task.CompletedTask;
        }

        var thisUser = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        if (thisUser == null)
            return Task.CompletedTask;

        var habbo = session.GetHabbo();

        if (Police.PoliceState.IsCuffed(habbo.Id))
        {
            session.SendWhisper("You cannot spit while you are cuffed.");
            return Task.CompletedTask;
        }

        if (Police.PoliceState.IsBeingEscorted(habbo.Id))
        {
            session.SendWhisper("You cannot spit while you are being escorted.");
            return Task.CompletedTask;
        }

        var hurts = !room.IsSafeZone;
        if (hurts)
        {
            habbo.EnsureRpStatsLoaded();
            target.EnsureRpStatsLoaded();

            if (habbo.IsRpPassive)
            {
                session.SendWhisper("You cannot spit while you are passive.");
                return Task.CompletedTask;
            }

            if (target.IsRpPassive)
            {
                session.SendWhisper($"{target.Username} is passive and cannot be spat at.");
                return Task.CompletedTask;
            }

            if (target.RpHealth <= 0)
            {
                session.SendWhisper($"{target.Username} is already out cold.");
                return Task.CompletedTask;
            }
        }

        if (_lastSpit.TryGetValue(habbo.Id, out var last))
        {
            var elapsed = (DateTime.UtcNow - last).TotalSeconds;
            if (elapsed < CooldownSeconds)
            {
                var remaining = (int)Math.Ceiling(CooldownSeconds - elapsed);
                session.SendWhisper($"Cooldown [{remaining}/{CooldownSeconds}]");
                return Task.CompletedTask;
            }
        }

        var landed = InReach(thisUser, targetUser);

        // The cooldown is spent either way. A miss is a turn taken, not a
        // free retry - otherwise the right move is to spam it from across the
        // room until somebody wanders into range.
        _lastSpit[habbo.Id] = DateTime.UtcNow;

        // Where the mess ends up: their tile if it carried, the spitter's own
        // if it did not.
        var splatUser = landed ? targetUser : thisUser;
        DropSplat(room, habbo, splatUser.X, splatUser.Y);

        // Leading AND trailing "*": the client only reads a style-4 bubble as an
        // action when the text is wrapped in them, and it moves the opening
        // marker ahead of the actor's name - "*Actor spits at Target*".
        //
        // Only a spit that lands AND hurts names a cost. In a safe zone it is
        // flavour, and a miss never hurt anybody but the spitter's dignity.
        room.SendPacket(new ChatComposer(thisUser.VirtualId, landed
            ? (hurts
                ? $"*spits at {target.Username}, causing {Damage} damage*"
                : $"*spits at {target.Username}*")
            : $"*spits at {target.Username}, but it dribbles down their own chin*", 0, FightBubble));

        if (!landed || !hurts)
            return Task.CompletedTask;

        target.RpHealth = Math.Max(0, target.RpHealth - Damage);
        target.SaveRpStats();
        habbo.RpAggression = AggressionOnSpit;
        SendStats(room, targetUser, target);
        SendStats(room, thisUser, habbo);

        if (target.RpHealth <= 0)
            room.GetRoomUserManager().ApplyRpKnockout(targetUser);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Put a splat on a tile for <see cref="SplatLifetime"/>, then take it away.
    ///
    /// The item never reaches the database or the room's own item list, so it
    /// is invisible to everything server-side: it cannot be picked up, moved,
    /// walked into or saved. It is two packets and a timer.
    ///
    /// Silently does nothing if the furni is missing from the library rather
    /// than throwing - a social command should not die on a hotel that has not
    /// imported one piece of Christmas furniture.
    /// </summary>
    private void DropSplat(Room room, Habbo owner, int x, int y)
    {
        var definition = _itemDataManager?.GetItemByName(SplatItemName);
        if (definition == null)
            return;

        var item = new Item
        {
            Id = Interlocked.Decrement(ref _ghostId),
            OwnerId = (uint)owner.Id,
            RoomId = room.RoomId,
            Definition = definition,
            ExtraData = new LegacyDataFormat { Data = string.Empty },
            Username = owner.Username,
            GetX = x,
            GetY = y,
            GetZ = room.GetGameMap().SqAbsoluteHeight(x, y),
            Rotation = 0
        };

        room.SendPacket(new ObjectAddComposer(item));

        // Fire and forget: the command must not wait four seconds to return.
        // The room is re-checked on the far side because it can be emptied and
        // unloaded while the splat is still on the floor.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SplatLifetime);

                if (!room.MDisposed)
                    room.SendPacket(new ObjectRemoveComposer(item, owner.Id));
            }
            catch
            {
                // A splat that fails to clear is a cosmetic leftover on one
                // client until it reloads the room. Never worth taking a
                // background thread down for.
            }
        });
    }

    /// <summary>
    /// The same firing line as StunCommand.InReach: straight along a row or
    /// column for two tiles, one tile on a true diagonal, nothing off those
    /// lines. Kept in step with it by hand - if :stun's reach changes, this
    /// should change with it.
    /// </summary>
    private static bool InReach(RoomUser spitter, RoomUser target)
    {
        var dx = Math.Abs(target.X - spitter.X);
        var dy = Math.Abs(target.Y - spitter.Y);
        if (dx == 0 && dy == 0)
            return true;
        if (dx == 0 || dy == 0)
            return Math.Max(dx, dy) <= StraightReach;
        if (dx == dy)
            return dx == 1;
        return false;
    }

    private static void SendStats(Room room, RoomUser user, Habbo habbo) =>
        room.SendPacket(new RpStatsComposer(user.VirtualId, habbo.RpHealth, habbo.RpHealthMax, habbo.RpEnergy, habbo.RpEnergyMax,
            (int)Math.Round(habbo.RpAggression), habbo.IsRpPassive ? 1 : 0, habbo.Rank >= 5 ? 1 : 0));
}
