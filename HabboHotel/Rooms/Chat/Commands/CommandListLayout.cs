using System.Text;

namespace Plus.HabboHotel.Rooms.Chat.Commands;

/// <summary>
/// pixelrp: the groups the :commands window lists, in the order its side rail
/// shows them (Twist, 2026-10-18). A group names its commands by key; a
/// command no group names falls back to the group for its folder (see
/// <see cref="FolderGroup"/>), so a new command still turns up somewhere
/// sensible without an edit here.
///
/// The text keeps the old shape the client already reads - a marker line,
/// then a "[Group]" line before each group's ":key params - description"
/// lines - with staff groups written "[Staff: Group]" so the window can put
/// them under its Staff heading (and offer "All"). Only staff are sent those:
/// a player who can use a staff command anyway (VIP :wd, an unlocked command)
/// finds it in a player group instead.
/// </summary>
internal static class CommandListLayout
{
    /// <summary>The client tells the :commands list from every other MOTD by this first line.</summary>
    public const string Marker = "This is the list of commands you have available:";

    /// <summary>Trial Moderator: "all staff", as in the commands clean-up (275-277).</summary>
    public const int StaffRank = 2;

    private sealed class Group
    {
        public Group(string name, bool staff, bool alphabetical, params string[] keys) : this(name, staff, alphabetical, null, keys) { }

        public Group(string name, bool staff, bool alphabetical, Group playerHome, params string[] keys)
        {
            Name = name;
            Staff = staff;
            Alphabetical = alphabetical;
            PlayerHome = playerHome;
            Keys = keys;
        }

        public string Name { get; }
        public bool Staff { get; }

        /// <summary>A staff group's commands, for a player who can use one of them.</summary>
        public Group PlayerHome { get; }

        /// <summary>
        /// False: the keys below in the order given (a player reads them as a
        /// list of things to do), then anything else alphabetically. True: the
        /// whole group alphabetically.
        /// </summary>
        public bool Alphabetical { get; }

        public string[] Keys { get; }
    }

    private static readonly Group General = new("General", false, false,
        "sit", "stand", "lay", "stats", "passive", "suicide", "flagme", "mutebots", "mutepets");

    private static readonly Group JobsAndGangs = new("Jobs & gangs", false, false,
        "startwork", "stopwork", "hire", "fire", "quitjob", "ca", "heal", "ga");

    private static readonly Group Trading = new("Trading", false, false, "give", "offer");

    private static readonly Group Fighting = new("Fighting", false, false, "hit", "slap", "push", "pull");

    private static readonly Group Social = new("Social", false, false, "hug", "kiss", "bite", "propose", "divorce", "dance");

    private static readonly Group Police = new("Police", false, false,
        "911", "backup", "calls", "stun", "ps", "fb", "cuff", "uncuff", "escort", "unescort", "charge", "pardon", "arrest");

    private static readonly Group RoomAndFurni = new("Room & Furni", false, false,
        "bh", "rot", "undo", "pickall", "kickpets");

    // The room commands only staff can use: their own group under Staff, not
    // a tag inside the player one.
    private static readonly Group StaffRoomAndFurni = new("Room & Furni", true, true, RoomAndFurni,
        "ejectall", "kickbots", "regenmaps", "setspeed", "ss", "unload");

    // Moderator/Fun, plus the User/Fun commands made staff-only in the clean-up.
    private static readonly Group StaffFun = new("Staff fun", true, true, Social,
        "enable", "faceless", "follow", "mimic", "spit", "spush");

    private static readonly Group Moderator = new("Moderator", true, true, General);

    private static readonly Group Administrator = new("Administrator", true, true, General);

    private static readonly Group[] Groups =
    {
        General, JobsAndGangs, Trading, Fighting, Social, Police, RoomAndFurni,
        StaffRoomAndFurni, StaffFun, Moderator, Administrator
    };

    private static readonly Dictionary<string, Group> ByKey = Groups
        .SelectMany(group => group.Keys.Select(key => (key, group)))
        .ToDictionary(entry => entry.key, entry => entry.group, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Where a command no group names goes, from its folder under Commands.
    /// Anything under User is a player command; anything else is staff.
    /// </summary>
    private static Group FolderGroup(ICommandBase command)
    {
        const string root = ".Commands.";
        var ns = command.GetType().Namespace ?? string.Empty;
        var at = ns.IndexOf(root, StringComparison.Ordinal);
        var folder = at < 0 ? string.Empty : ns[(at + root.Length)..];
        return folder switch
        {
            "User.Fight" => Fighting,
            "User.Fun" or "User.Social" => Social,
            "User.Police" => Police,
            "Moderator.Fun" => StaffFun,
            "Administrator" => Administrator,
            _ when folder.StartsWith("User", StringComparison.Ordinal) => General,
            _ => Moderator
        };
    }

    /// <summary>The :commands text for the commands this player can use.</summary>
    public static string Build(IEnumerable<ICommandBase> usable, bool staff)
    {
        var members = Groups.ToDictionary(group => group, _ => new List<ICommandBase>());
        foreach (var command in usable.Distinct())
        {
            var group = ByKey.TryGetValue(command.Key, out var named) ? named : FolderGroup(command);
            if (group.Staff && !staff)
                group = group.PlayerHome;
            members[group].Add(command);
        }

        var text = new StringBuilder(Marker).Append('\n');
        foreach (var group in Groups)
        {
            var commands = members[group];
            if (commands.Count == 0)
                continue;
            var ordered = group.Alphabetical
                ? commands.OrderBy(c => c.Key, StringComparer.Ordinal)
                : commands.OrderBy(c => Array.IndexOf(group.Keys, c.Key) is var i && i >= 0 ? i : int.MaxValue)
                    .ThenBy(c => c.Key, StringComparer.Ordinal);
            text.Append(group.Staff ? $"[Staff: {group.Name}]\n" : $"[{group.Name}]\n");
            foreach (var command in ordered)
                text.Append($":{command.Key} {command.Parameters} - {command.Description}\n");
        }
        return text.ToString();
    }
}
