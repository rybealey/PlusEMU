using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Speech;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp stress testing: spawn ephemeral freeroaming NPC clones of a player
/// to load-test the room cycle, pathfinder, and movement broadcast. Zombies
/// live only in memory - negative bot ids, no DB rows - so bare :zombies
/// removes every one of them without touching real placed bots.
/// </summary>
internal class ZombiesCommand : IChatCommand
{
    private const int MaxZombiesPerRoom = 100;

    // Negative ids keep zombies clear of real (DB-backed) bot ids in
    // RoomUserManager._bots (keyed by BotData.Id, so duplicates would
    // silently overwrite) and double as the "is a zombie" marker.
    private static int _nextZombieId;

    public string Key => "zombies";
    public string PermissionRequired => "command_zombies";

    public string Parameters => "%username% %quantity%";

    public string Description => "Stress test: spawn freeroaming clones of a player. Bare :zombies removes them all.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        // CommandManager splits on ' ' without removing empties, so a
        // trailing space (or a bare ":zombies ") yields ["", ...] rather
        // than an empty array - treat that the same as no parameters.
        if (parameters.All(string.IsNullOrEmpty))
        {
            DespawnAll(session, room);
            return;
        }
        if (parameters.Length < 2 || !int.TryParse(parameters[1], out var quantity) || quantity <= 0)
        {
            session.SendWhisper("Usage: :zombies <username> <quantity> to spawn, :zombies to remove them all.");
            return;
        }
        var target = room.GetRoomUserManager().GetRoomUserByHabbo(parameters[0]);
        var habbo = target?.GetClient()?.GetHabbo();
        if (habbo == null)
        {
            session.SendWhisper("That user is not in this room.");
            return;
        }
        var existing = CountZombies(room);
        var toSpawn = Math.Min(quantity, MaxZombiesPerRoom - existing);
        if (toSpawn <= 0)
        {
            session.SendWhisper($"This room is already at the zombie cap ({MaxZombiesPerRoom}). Use :zombies to clear them first.");
            return;
        }
        // Sharing one empty list is safe: RoomBot only reads it at construction.
        var emptySpeech = new List<RandomSpeech>();
        // All zombies enter through the room's door tile, same as a real
        // avatar walking in, then disperse on their own via freeroam AI.
        var model = room.GetGameMap().Model;
        for (var i = 0; i < toSpawn; i++)
        {
            var bot = new RoomBot(Interlocked.Decrement(ref _nextZombieId), room.RoomId, "generic", "freeroam",
                habbo.Username, "", habbo.Look, model.DoorX, model.DoorY, model.DoorZ, 0, 0, 0, 0, 0,
                ref emptySpeech, habbo.Gender, 0, session.GetHabbo().Id, false, 0, false, 0);
            // DeployBot can relocate a bot whose requested coords fail its
            // bounds check, so register the map at the ACTUAL deployed
            // position rather than the requested one.
            var zombie = room.GetRoomUserManager().DeployBot(bot, null);
            room.GetGameMap().UpdateUserMovement(new(zombie.X, zombie.Y), new(zombie.X, zombie.Y), zombie);
        }
        var total = existing + toSpawn;
        session.SendWhisper(toSpawn < quantity
            ? $"Spawned {toSpawn} of {quantity} zombies cloning {habbo.Username} (hit the cap: {total}/{MaxZombiesPerRoom} in room)."
            : $"Spawned {toSpawn} zombie{(toSpawn == 1 ? "" : "s")} cloning {habbo.Username} ({total}/{MaxZombiesPerRoom} in room).");
    }

    private static void DespawnAll(GameClient session, Room room)
    {
        var zombies = room.GetRoomUserManager().GetUserList().ToList()
            .Where(IsZombie)
            .ToList();
        foreach (var zombie in zombies)
        {
            // RemoveBot alone leaves a phantom occupant behind: it ends in
            // OnRemove(user), which early-returns for bots (no GetClient()),
            // so RemoveUserFromMap never runs and the tile stays blocked
            // until room unload. Clear the map entry ourselves first, same
            // as PickUpBotEvent.cs does for player-picked-up bots.
            room.GetGameMap().RemoveUserFromMap(zombie, new(zombie.X, zombie.Y));
            room.GetRoomUserManager().RemoveBot(zombie.VirtualId, false);
        }
        // RemoveBot can silently no-op (e.g. the user vanished from
        // RoomUserManager between the snapshot above and now), so recount
        // rather than trusting the pre-removal list length.
        var removed = zombies.Count - CountZombies(room);
        session.SendWhisper(removed == 0
            ? "No zombies in this room."
            : $"Removed {removed} zombie{(removed == 1 ? "" : "s")}.");
    }

    private static int CountZombies(Room room) =>
        room.GetRoomUserManager().GetUserList().ToList().Count(IsZombie);

    /// <summary>
    /// Also used by ServerStatusUpdater so zombie clones count toward the
    /// hotel's reported online user total.
    /// </summary>
    internal static bool IsZombie(RoomUser user) =>
        user is { IsBot: true, IsPet: false, BotData.Id: < 0 };
}
