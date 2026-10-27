using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Speech;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

/// <summary>
/// pixelrp stress testing: spawn ephemeral freeroaming NPC clones of a player
/// to load-test the room cycle, pathfinder, and movement broadcast. Zombies
/// live only in memory - negative bot ids, no DB rows - so bare :zombies
/// removes every one of them without touching real placed bots.
///
/// pixelrp (Twist, 2026-10-27): a crowd that talks, for lag tests of a busy
/// room with the lag recorder running. A third number makes each zombie say a
/// line about every that many seconds (spread so they never speak in step),
/// and "*" in place of a name dresses each one as a different real player,
/// in that player's chat bubble - a crowd of many outfits and bubbles, not
/// one look many times over. :massdance sets them dancing too.
/// </summary>
internal class ZombiesCommand : IChatCommand
{
    private const int MaxZombiesPerRoom = 100;

    /// <summary>The longest gap between a talking zombie's lines, in seconds.</summary>
    private const int MaxTalkSeconds = 120;

    /// <summary>
    /// What a talking zombie says - short and long, so bubbles of every width
    /// and wrapped lines are drawn, the way a busy street's chat looks.
    /// </summary>
    private static readonly string[] Lines =
    {
        "hey",
        "lol",
        "anyone selling a bat?",
        "brb",
        "who's on duty at the hospital right now?",
        "meet me outside the bank in five",
        "*leans against the wall and checks their phone*",
        "that's crazy",
        "I just got out of jail, never again",
        "does anyone know where the gym is from here? I keep getting lost on the way",
        "*walks over and waves at everyone in the room*",
        "how much for the vest?",
        "no way",
        "the police are coming this way, act normal",
        "has anyone seen the new furni in the shop today, the board game table looks so good",
        "ok",
        "*sits down for a second, out of breath after running all the way from the city centre*",
        "who's in a gang here?",
        "I need a job, is anyone hiring?",
        "haha",
        "did you hear what happened at the courthouse earlier? the whole jury walked out",
        "see you later",
        "*laughs*",
        "can someone heal me please, I'm on 20 health"
    };

    // Negative ids keep zombies clear of real (DB-backed) bot ids in
    // RoomUserManager._bots (keyed by BotData.Id, so duplicates would
    // silently overwrite) and double as the "is a zombie" marker.
    private static int _nextZombieId;

    public string Key => "zombies";
    public string PermissionRequired => "command_zombies";

    public string Parameters => "%username or *% %quantity% %talk seconds%";

    public string Description => "Stress test: spawn freeroaming clones of a player (* for a crowd of players' looks); a third number makes them talk every that many seconds. Bare :zombies removes them all.";

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
        var talkSeconds = 0;
        if (parameters.Length < 2 || !int.TryParse(parameters[1], out var quantity) || quantity <= 0
            || (parameters.Length > 2 && !string.IsNullOrEmpty(parameters[2]) && (!int.TryParse(parameters[2], out talkSeconds) || talkSeconds < 1 || talkSeconds > MaxTalkSeconds)))
        {
            session.SendWhisper($"Usage: :zombies <username or *> <quantity> [talk seconds, 1-{MaxTalkSeconds}] to spawn, :zombies to remove them all.");
            return;
        }
        // "*": a crowd dressed as the hotel's players; otherwise clones of one
        // player in this room
        var crowd = parameters[0] == "*";
        List<(string Name, string Look, string Gender, int Bubble)> looks;
        if (crowd)
        {
            looks = PlayerLooks();
            if (looks.Count == 0)
            {
                session.SendWhisper("No player looks to dress a crowd in.");
                return;
            }
        }
        else
        {
            var target = room.GetRoomUserManager().GetRoomUserByHabbo(parameters[0]);
            var habbo = target?.GetClient()?.GetHabbo();
            if (habbo == null)
            {
                session.SendWhisper("That user is not in this room.");
                return;
            }
            looks = new() { (habbo.Username, habbo.Look, habbo.Gender, Math.Max(0, habbo.CustomBubbleId)) };
        }
        var existing = CountZombies(room);
        var toSpawn = Math.Min(quantity, MaxZombiesPerRoom - existing);
        if (toSpawn <= 0)
        {
            session.SendWhisper($"This room is already at the zombie cap ({MaxZombiesPerRoom}). Use :zombies to clear them first.");
            return;
        }
        // All zombies enter through the room's door tile, same as a real
        // avatar walking in, then disperse on their own via freeroam AI.
        var model = room.GetGameMap().Model;
        for (var i = 0; i < toSpawn; i++)
        {
            var id = Interlocked.Decrement(ref _nextZombieId);
            var (name, look, gender, bubble) = looks[i % looks.Count];
            // RoomBot keeps only the lines carrying its own id.
            var speech = talkSeconds > 0 ? Lines.Select(line => new RandomSpeech(line, id)).ToList() : new List<RandomSpeech>();
            // ticks of 500 ms, each zombie between half and one and a half
            // times the gap asked for, so the crowd never talks in step
            var interval = talkSeconds > 0 ? Math.Max(1, (int)Math.Round(talkSeconds * 2 * (0.5 + Random.Shared.NextDouble()))) : 0;
            var bot = new RoomBot(id, room.RoomId, "generic", "freeroam",
                crowd ? $"Zombie {-id}" : name, "", look, model.DoorX, model.DoorY, model.DoorZ, 0, 0, 0, 0, 0,
                ref speech, gender, 0, session.GetHabbo().Id, talkSeconds > 0, interval, false, talkSeconds > 0 ? bubble : 0);
            // DeployBot can relocate a bot whose requested coords fail its
            // bounds check, so register the map at the ACTUAL deployed
            // position rather than the requested one.
            var zombie = room.GetRoomUserManager().DeployBot(bot, null);
            room.GetGameMap().UpdateUserMovement(new(zombie.X, zombie.Y), new(zombie.X, zombie.Y), zombie);
        }
        var total = existing + toSpawn;
        var what = crowd ? $"in {Math.Min(toSpawn, looks.Count)} players' looks" : $"cloning {looks[0].Name}";
        var talk = talkSeconds > 0 ? $", talking about every {talkSeconds} second{(talkSeconds == 1 ? "" : "s")}" : "";
        session.SendWhisper(toSpawn < quantity
            ? $"Spawned {toSpawn} of {quantity} zombies {what}{talk} (hit the cap: {total}/{MaxZombiesPerRoom} in room)."
            : $"Spawned {toSpawn} zombie{(toSpawn == 1 ? "" : "s")} {what}{talk} ({total}/{MaxZombiesPerRoom} in room).");
    }

    /// <summary>The players' looks and chat bubbles a "*" crowd is dressed in, shuffled.</summary>
    private static List<(string Name, string Look, string Gender, int Bubble)> PlayerLooks()
    {
        var looks = new List<(string Name, string Look, string Gender, int Bubble)>();
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `username`, `look`, `gender`, `bubble_id` FROM `users` WHERE `look` <> '' ORDER BY RAND() LIMIT @limit");
        dbClient.AddParameter("limit", MaxZombiesPerRoom);
        var table = dbClient.GetTable();
        if (table == null)
            return looks;
        foreach (System.Data.DataRow row in table.Rows)
            looks.Add((Convert.ToString(row["username"]) ?? "", Convert.ToString(row["look"]) ?? "", Convert.ToString(row["gender"]) ?? "M", BubbleOf(Convert.ToString(row["bubble_id"]))));
        return looks;
    }

    /// <summary>A player's saved chat bubble as a bot's: 0, the plain one, when none is saved.</summary>
    private static int BubbleOf(string? bubbleId) => int.TryParse(bubbleId, out var bubble) && bubble > 0 ? bubble : 0;

    private static void DespawnAll(GameClient session, Room room)
    {
        var zombies = room.GetRoomUserManager().GetUserList().ToList()
            .Where(IsZombie)
            .ToList();
        foreach (var zombie in zombies)
        {
            // RemoveBot takes the zombie off the tile map itself now - it used
            // to leave a phantom occupant behind, which this cleared by hand.
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
