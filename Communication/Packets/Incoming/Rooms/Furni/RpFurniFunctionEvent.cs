using System.Globalization;
using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// pixelrp: applies a behaviour change to a furni DEFINITION, hotel-wide.
///
/// Three things have to happen together, and the order matters:
///
/// 1. `furniture` is updated, so the change survives a restart.
/// 2. The live ItemDefinition is MUTATED IN PLACE. Every placed Item holds a
///    reference to the same definition object, so mutating it updates every
///    copy in every loaded room at once. This is also why `:update items` is
///    the wrong tool here - ItemDataManager.Init() clears and rebuilds the
///    dictionary, leaving already-placed items pointing at orphaned
///    definitions that no longer change with the database.
/// 3. Every loaded room holding one regenerates its map, because walkability
///    and seating are baked into Gamemap at generation time, not read per
///    step.
///
/// Then the new record goes to everyone online: the client keeps its own copy
/// of these flags in FurnitureData, and nitro-renderer will not compute a walk
/// target for a furni whose canStandOn/canSitOn/canLayOn are all false. A
/// server-only change would leave a tile the server paths to but that nobody
/// can reach by clicking the furni.
///
/// Size, sprite and floor/wall are deliberately NOT editable: those come from
/// the furni's artwork, and changing them here would make the server block
/// tiles the sprite never covers.
/// </summary>
internal class RpFurniFunctionEvent : IPacketEvent
{
    private const int MaximumHeight = 4000; // hundredths; the stack-height widget's ceiling is 40
    private const int MaximumModes = 128;

    private readonly IDatabase _database;
    private readonly IRoomManager _roomManager;
    private readonly IGameClientManager _clientManager;

    public RpFurniFunctionEvent(IDatabase database, IRoomManager roomManager, IGameClientManager clientManager)
    {
        _database = database;
        _roomManager = roomManager;
        _clientManager = clientManager;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var definitionId = packet.ReadInt();
        var walkable = packet.ReadBool();
        var walkMask = SanitiseMask(packet.ReadString());
        var seat = packet.ReadBool();
        var stackable = packet.ReadBool();
        var heightHundredths = Math.Clamp(packet.ReadInt(), 0, MaximumHeight);
        var adjustableHeights = ParseDoubleList(packet.ReadString());
        var interactionName = (packet.ReadString() ?? string.Empty).Trim().ToLowerInvariant();
        var modes = Math.Clamp(packet.ReadInt(), 1, MaximumModes);
        var effectId = Math.Max(0, packet.ReadInt());
        var behaviourData = Math.Max(0, packet.ReadInt());
        var vendingIds = ParseIntList(packet.ReadString());

        var habbo = session.GetHabbo();
        if (habbo == null || !habbo.Permissions.HasCommand("rp_furni_function"))
            return Task.CompletedTask;

        var room = habbo.CurrentRoom;
        if (room == null)
            return Task.CompletedTask;

        // Resolved through the room the caller is standing in, so a client
        // cannot name an arbitrary definition id it never had in front of it.
        var item = room.GetRoomItemHandler().GetFloor.FirstOrDefault(x => x.Definition?.Id == (uint)definitionId)
                   ?? room.GetRoomItemHandler().GetWall.FirstOrDefault(x => x.Definition?.Id == (uint)definitionId);
        var definition = item?.Definition;
        if (definition == null)
            return Task.CompletedTask;

        // An unrecognised name would silently become InteractionType.None and
        // quietly strip whatever the furni did, so it is rejected instead.
        if (string.IsNullOrEmpty(interactionName))
            interactionName = "default";
        var interactionType = InteractionTypes.GetTypeFromString(interactionName);
        if (interactionType == InteractionType.None && interactionName != "default")
            return Task.CompletedTask;

        var height = heightHundredths / 100d;
        var changes = new List<(string Field, string Old, string New)>();
        void Track(string field, string oldValue, string newValue)
        {
            if (oldValue != newValue)
                changes.Add((field, oldValue, newValue));
        }

        Track("is_walkable", Bit(definition.Walkable), Bit(walkable));
        Track("walk_mask", definition.WalkMask ?? string.Empty, walkMask);
        Track("can_sit", Bit(definition.IsSeat), Bit(seat));
        Track("can_stack", Bit(definition.Stackable), Bit(stackable));
        Track("stack_height", Num(definition.Height), Num(height));
        Track("height_adjustable", Join(definition.AdjustableHeights), Join(adjustableHeights));
        Track("interaction_type", definition.InteractionTypeName ?? "default", interactionName);
        Track("interaction_modes_count", definition.Modes.ToString(), modes.ToString());
        Track("effect_id", definition.EffectId.ToString(), effectId.ToString());
        Track("behaviour_data", definition.BehaviourData.ToString(), behaviourData.ToString());
        Track("vending_ids", Join(definition.VendingIds), Join(vendingIds));

        if (changes.Count == 0)
            return Task.CompletedTask;

        using (var dbClient = _database.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `furniture` SET `is_walkable` = @walkable, `walk_mask` = @walkMask, `can_sit` = @seat, " +
                              "`can_stack` = @stackable, `stack_height` = @height, `height_adjustable` = @adjustable, " +
                              "`interaction_type` = @interaction, `interaction_modes_count` = @modes, " +
                              "`effect_id` = @effect, `behaviour_data` = @behaviour, `vending_ids` = @vending " +
                              "WHERE `id` = @definitionId LIMIT 1");
            dbClient.AddParameter("walkable", Bit(walkable));
            dbClient.AddParameter("walkMask", walkMask);
            dbClient.AddParameter("seat", Bit(seat));
            dbClient.AddParameter("stackable", Bit(stackable));
            dbClient.AddParameter("height", height);
            dbClient.AddParameter("adjustable", adjustableHeights.Count > 0 ? Join(adjustableHeights) : "0");
            dbClient.AddParameter("interaction", interactionName);
            dbClient.AddParameter("modes", modes);
            dbClient.AddParameter("effect", effectId);
            dbClient.AddParameter("behaviour", behaviourData);
            dbClient.AddParameter("vending", vendingIds.Count > 0 ? Join(vendingIds) : "0");
            dbClient.AddParameter("definitionId", definitionId);
            dbClient.RunQuery();

            foreach (var change in changes)
            {
                dbClient.SetQuery("INSERT INTO `rp_furni_function_log` (`definition_id`, `item_name`, `user_id`, " +
                                  "`username`, `field`, `old_value`, `new_value`) VALUES (@definitionId, @itemName, " +
                                  "@userId, @username, @field, @oldValue, @newValue)");
                dbClient.AddParameter("definitionId", definitionId);
                dbClient.AddParameter("itemName", definition.ItemName ?? string.Empty);
                dbClient.AddParameter("userId", habbo.Id);
                dbClient.AddParameter("username", habbo.Username ?? string.Empty);
                dbClient.AddParameter("field", change.Field);
                dbClient.AddParameter("oldValue", change.Old);
                dbClient.AddParameter("newValue", change.New);
                dbClient.RunQuery();
            }
        }

        // In place, not a reload - see the class note.
        definition.Walkable = walkable;
        definition.WalkMask = walkMask;
        definition.IsSeat = seat;
        definition.Stackable = stackable;
        definition.Height = height;
        definition.AdjustableHeights = adjustableHeights;
        definition.InteractionType = interactionType;
        definition.InteractionTypeName = interactionName;
        definition.Modes = modes;
        definition.EffectId = effectId;
        definition.BehaviourData = behaviourData;
        definition.VendingIds = vendingIds;

        foreach (var loaded in _roomManager.GetRooms())
        {
            var handler = loaded?.GetRoomItemHandler();
            if (handler == null)
                continue;
            if (handler.GetFloor.Any(x => x.Definition?.Id == definition.Id) ||
                handler.GetWall.Any(x => x.Definition?.Id == definition.Id))
                loaded.GetGameMap().GenerateMaps();
        }

        _clientManager.SendPacket(new RpFurniFunctionComposer(definition));
        return Task.CompletedTask;
    }

    /// A mask the map cannot read is worse than none - it would punch holes in
    /// a wall on a shape nobody intended - so anything that is not exactly the
    /// footprint's worth of 0s and 1s, or is all zeroes (which says nothing the
    /// furni's own flag does not), comes back as no mask at all.
    private static string SanitiseMask(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        raw = raw.Trim();
        if (raw.Length > 64 || raw.Any(c => c != '0' && c != '1') || raw.All(c => c == '0'))
            return string.Empty;
        return raw;
    }

    private static string Bit(bool value) => value ? "1" : "0";

    private static string Num(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Join(IEnumerable<double> values) =>
        string.Join(",", values.Select(x => x.ToString("0.####", CultureInfo.InvariantCulture)));

    private static string Join(IEnumerable<int> values) => string.Join(",", values);

    private static List<int> ParseIntList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "0")
            return new List<int>(0);
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var value) ? value : -1)
            .Where(value => value >= 0)
            .Take(64)
            .ToList();
    }

    private static List<double> ParseDoubleList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "0")
            return new List<double>(0);
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : -1)
            .Where(value => value >= 0 && value <= MaximumHeight / 100d)
            .Take(32)
            .ToList();
    }
}
