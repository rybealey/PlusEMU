using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police actions: :charge &lt;player&gt; &lt;crime&gt; - put a crime on
/// someone's rap sheet.
///
/// The crimes themselves are data, managed in /housekeeping (rp_crimes): an
/// officer types the short key, not the display name, so `:charge Yavn gta`
/// files Grand Theft Auto. Read from the database on every use rather than
/// cached, so a crime edited in housekeeping is live at once - it is one
/// indexed lookup on a table with a few dozen rows.
///
/// A charge is a record, not a state: `rp_charges` gains a row per count and
/// nothing is ever deleted by charging again. Dropping a charge sets
/// `dropped_at`, which is a housekeeping job rather than a chat command.
///
/// Whether the same crime can sit on a sheet twice is the crime's own
/// `stackable` flag. A non-stackable one already on the sheet is refused
/// rather than duplicated - driving unlicensed is a state, not a tally.
///
/// Announced in the room, not whispered. An arrest is public theatre and the
/// people watching are the point; the officer gets the only private line,
/// when the charge does not land.
/// </summary>
internal class ChargeCommand : ITargetChatCommand
{
    public string Key => "charge";

    public string PermissionRequired => "command_charge";

    public string Parameters => "%target% %crime%";

    public string Description => "Charge another user with a crime.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "No target selected.";

    /// <summary>Blue bubble, the one every police action shares.</summary>
    private const int PoliceBubble = 4;

    private sealed class Crime
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public bool Stackable { get; init; }
    }

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        // Police powers are a job: on the force AND clocked in.
        if (!PoliceUtility.RequireOnDuty(session, "charge someone"))
            return Task.CompletedTask;

        if (target == habbo)
        {
            session.SendWhisper("You cannot charge yourself.");
            return Task.CompletedTask;
        }

        // The target has to be here to be charged - the same rule the rest of
        // the police commands follow, so a sheet cannot be filled from across
        // the hotel.
        if (room.GetRoomUserManager().GetRoomUserByHabbo(target.Id) == null)
        {
            session.SendWhisper($"{target.Username} is not in this room.");
            return Task.CompletedTask;
        }

        var key = (parameters.Length > 0 ? parameters[0] : "").Trim().ToLowerInvariant();
        if (key.Length == 0)
        {
            session.SendWhisper("Which crime? Try :charge " + target.Username + " <crime>.");
            return Task.CompletedTask;
        }

        var crime = FindCrime(key);
        if (crime == null)
        {
            session.SendWhisper($"There is no crime called \"{key}\".");
            return Task.CompletedTask;
        }

        if (!crime.Stackable && HasOpenCharge(target.Id, crime.Id))
        {
            session.SendWhisper($"{target.Username} is already charged with {crime.Name}.");
            return Task.CompletedTask;
        }

        // Resolved before the write: an officer with no room unit cannot be
        // announced, and filing a charge nobody in the room saw happen is
        // worse than refusing it.
        var officerUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (officerUser == null)
            return Task.CompletedTask;

        File(target.Id, crime.Id, habbo.Id);

        room.SendPacket(new ChatComposer(officerUser.VirtualId,
            $"*charges {target.Username} with {crime.Name}*", 0, PoliceBubble));
        return Task.CompletedTask;
    }

    private static Crime FindCrime(string key)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT `id`, `name`, `stackable` FROM `rp_crimes` " +
                          "WHERE `key_name` = @key AND `active` = 1 LIMIT 1");
        dbClient.AddParameter("key", key);
        var row = dbClient.GetRow();
        if (row == null)
            return null;
        return new Crime
        {
            Id = Convert.ToInt32(row["id"]),
            Name = Convert.ToString(row["name"]) ?? "",
            Stackable = Convert.ToInt32(row["stackable"]) == 1
        };
    }

    private static bool HasOpenCharge(int userId, int crimeId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT 1 FROM `rp_charges` WHERE `user_id` = @user AND `crime_id` = @crime " +
                          "AND `dropped_at` = 0 LIMIT 1");
        dbClient.AddParameter("user", userId);
        dbClient.AddParameter("crime", crimeId);
        return dbClient.GetRow() != null;
    }

    private static void File(int userId, int crimeId, int officerId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("INSERT INTO `rp_charges` (`user_id`,`crime_id`,`officer_id`,`charged_at`) " +
                          "VALUES (@user,@crime,@officer,UNIX_TIMESTAMP())");
        dbClient.AddParameter("user", userId);
        dbClient.AddParameter("crime", crimeId);
        dbClient.AddParameter("officer", officerId);
        dbClient.RunQuery();
    }
}
