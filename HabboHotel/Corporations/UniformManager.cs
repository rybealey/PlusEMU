using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Dapper;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands.User.Police;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Corporations;

/// <summary>
/// pixelrp: uniforms - a corporation rank's outfit worn while clocked in, and
/// the prisoner's outfit worn while serving (rp_uniforms, migration 233). Set
/// in the City Panel's Uniforms tab.
///
/// A uniform is only clothing (<see cref="ClothingTypes"/>): it replaces what
/// the player wears and keeps their hair and face. It is applied IN MEMORY
/// ONLY - the working-motto pattern (ShiftManager.ApplyMotto): users.look keeps
/// the player's own look, so a relog or a crash always gives it back. While a
/// uniform is on, nothing may save the worn look as the player's own
/// (<see cref="RefuseChange"/>): Change Looks, a mannequin, :mimic, :faceless.
///
/// Which one, if any: in jail, the prisoner's; else on duty, their rank's;
/// else their own. A rank or gender with no uniform means their own clothes.
/// </summary>
public static class UniformManager
{
    public const string KindRank = "rank";
    public const string KindPrisoner = "prisoner";

    /// <summary>The set types a uniform supplies. Everything else - hair, face - stays the player's.</summary>
    public static readonly HashSet<string> ClothingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ha", "he", "ea", "fa", "ch", "cc", "cp", "ca", "lg", "sh", "wa"
    };

    /// <summary>A figure is never without these; a uniform that has none keeps the player's own.</summary>
    private static readonly string[] RequiredTypes = { "ch", "lg" };

    private const int MaxFigureLength = 512;

    private static readonly Regex PartPattern = new(@"^[a-z]{2}-\d{1,6}(-\d{1,6}){0,2}$", RegexOptions.Compiled);

    private sealed class UniformRow
    {
        public string Kind { get; set; } = "";
        public int RankId { get; set; }
        public string Gender { get; set; } = "";
        public string Figure { get; set; } = "";
    }

    private static ConcurrentDictionary<(string Kind, int RankId, string Gender), string>? _cache;

    private static ConcurrentDictionary<(string Kind, int RankId, string Gender), string> Cache
    {
        get
        {
            if (_cache != null)
                return _cache;
            var cache = new ConcurrentDictionary<(string, int, string), string>();
            using (var connection = PlusEnvironment.DatabaseManager.Connection())
                foreach (var row in connection.Query<UniformRow>("SELECT `kind` AS Kind, `rank_id` AS RankId, `gender` AS Gender, `figure` AS Figure FROM `rp_uniforms`"))
                    if (!string.IsNullOrEmpty(row.Figure))
                        cache[(row.Kind, row.RankId, row.Gender.ToUpperInvariant())] = row.Figure;
            _cache = cache;
            return cache;
        }
    }

    private static string NormaliseGender(string gender) => (gender ?? "").Trim().ToUpperInvariant() == "F" ? "F" : "M";

    /// <summary>A stored uniform, or "" when there is none.</summary>
    public static string Get(string kind, int rankId, string gender) =>
        Cache.TryGetValue((kind, kind == KindPrisoner ? 0 : rankId, NormaliseGender(gender)), out var figure) ? figure : "";

    /// <summary>Every (kind, rank, gender) that has a uniform.</summary>
    public static IEnumerable<(string Kind, int RankId, string Gender)> Defined() => Cache.Keys;

    /// <summary>
    /// Keep only well-formed clothing parts, one per set type. Returns "" for
    /// a figure with none - which, saved, means "no uniform".
    /// </summary>
    public static string Sanitise(string figure)
    {
        var parts = new Dictionary<string, string>();
        foreach (var raw in (figure ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = raw.Trim().ToLowerInvariant();
            if (!PartPattern.IsMatch(part))
                continue;
            var type = part[..2];
            if (ClothingTypes.Contains(type))
                parts[type] = part;
        }
        var result = string.Join(".", parts.Values);
        return result.Length > MaxFigureLength ? "" : result;
    }

    /// <summary>
    /// The player's own look wearing a uniform: their parts of every clothing
    /// type go, the uniform's come in. A uniform without a shirt or trousers
    /// leaves the player's own, so nobody is ever drawn without them.
    /// </summary>
    public static string Merge(string ownLook, string uniform)
    {
        var uniformParts = uniform.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var uniformTypes = uniformParts.Select(part => part.Split('-')[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = (ownLook ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries).Where(part =>
        {
            var type = part.Split('-')[0];
            if (!ClothingTypes.Contains(type))
                return true;
            return RequiredTypes.Contains(type, StringComparer.OrdinalIgnoreCase) && !uniformTypes.Contains(type);
        });
        return string.Join(".", kept.Concat(uniformParts));
    }

    /// <summary>The uniform this player should be wearing now, or "" for their own clothes.</summary>
    private static string DueFor(Habbo habbo)
    {
        if (JailState.IsJailed(habbo.Id))
            return Get(KindPrisoner, 0, habbo.Gender);
        var rankId = ShiftManager.RankOf(habbo.Id);
        return rankId > 0 ? Get(KindRank, rankId, habbo.Gender) : "";
    }

    /// <summary>
    /// Put on (or take off) whatever uniform the player is due, in memory, and
    /// say whether their look changed. Sends nothing: ShiftManager.ApplyMotto
    /// sends the look with the motto, and <see cref="Refresh"/> sends it
    /// everywhere else.
    /// </summary>
    public static bool Dress(Habbo? habbo)
    {
        if (habbo == null)
            return false;
        var due = DueFor(habbo);
        var before = habbo.Look;
        if (due.Length > 0)
        {
            // The look underneath is remembered once, when the first uniform
            // goes on - switching from a rank's to the prisoner's keeps it.
            if (!habbo.WearingUniform)
                habbo.OwnLook = habbo.Look;
            habbo.Look = Merge(habbo.OwnLook ?? habbo.Look, due);
            habbo.WearingUniform = true;
        }
        else if (habbo.WearingUniform)
        {
            habbo.Look = habbo.OwnLook ?? habbo.Look;
            habbo.OwnLook = null;
            habbo.WearingUniform = false;
        }
        return habbo.Look != before;
    }

    /// <summary>Dress the player and, if their look changed, show the room (and them).</summary>
    public static void Refresh(GameClient? client)
    {
        var habbo = client?.GetHabbo();
        if (!Dress(habbo))
            return;
        var room = habbo!.CurrentRoom;
        var roomUser = room?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (roomUser == null)
            return;
        client!.Send(new UserChangeComposer(roomUser, true));
        room!.SendPacket(new UserChangeComposer(roomUser, false));
    }

    /// <summary>
    /// A way of changing clothes that would save the worn look as the player's
    /// own. True, having told them, while a uniform is on.
    /// </summary>
    public static bool RefuseChange(GameClient? client)
    {
        if (client?.GetHabbo()?.WearingUniform != true)
            return false;
        client.SendWhisper("You can't change clothes while you're in uniform.");
        return true;
    }

    /// <summary>
    /// Save a uniform ("" removes it) and re-dress everyone wearing it right
    /// now: the on-duty players of that rank, or everyone in jail.
    /// </summary>
    public static void Save(string kind, int rankId, string gender, string figure, int staffId)
    {
        kind = kind == KindPrisoner ? KindPrisoner : KindRank;
        rankId = kind == KindPrisoner ? 0 : rankId;
        gender = NormaliseGender(gender);
        figure = Sanitise(figure);
        using (var connection = PlusEnvironment.DatabaseManager.Connection())
        {
            if (figure.Length == 0)
                connection.Execute("DELETE FROM `rp_uniforms` WHERE `kind` = @kind AND `rank_id` = @rankId AND `gender` = @gender",
                    new { kind, rankId, gender });
            else
                connection.Execute(
                    "INSERT INTO `rp_uniforms` (`kind`, `rank_id`, `gender`, `figure`, `updated_by`, `updated_at`) " +
                    "VALUES (@kind, @rankId, @gender, @figure, @staffId, UNIX_TIMESTAMP()) " +
                    "ON DUPLICATE KEY UPDATE `figure` = @figure, `updated_by` = @staffId, `updated_at` = UNIX_TIMESTAMP()",
                    new { kind, rankId, gender, figure, staffId });
        }
        if (figure.Length == 0)
            Cache.TryRemove((kind, rankId, gender), out _);
        else
            Cache[(kind, rankId, gender)] = figure;

        foreach (var client in PlusEnvironment.Game.ClientManager.GetClients.ToList())
        {
            var habbo = client?.GetHabbo();
            if (habbo == null || NormaliseGender(habbo.Gender) != gender)
                continue;
            var wears = kind == KindPrisoner ? JailState.IsJailed(habbo.Id) : ShiftManager.RankOf(habbo.Id) == rankId;
            if (wears)
                Refresh(client);
        }
    }
}
