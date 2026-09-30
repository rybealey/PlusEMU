using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.Movement;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Accounts;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police actions: :arrest &lt;player&gt; - book a suspect and send them
/// to jail (JailState).
///
/// The end of an arrest, not the start of one: by the time it is typed the
/// suspect has been charged, cuffed and walked in under escort. So all of that
/// is required, and one thing more - WHERE. Only at an Arrest Point: furni
/// staff have given the arrest_point behaviour (the booking desk of a
/// station), with the officer OR the suspect standing still on it. Under
/// escort the suspect walks a tile in front, so either works: the officer
/// steps onto the point, or brings the suspect onto it and stops one short.
/// The furni is the whole designation - any room with one placed books.
///
/// THE SENTENCE IS THE SHEET, summed and capped. Every open charge's
/// jail_seconds from housekeeping (rp_crimes), added up, rounded up to whole
/// minutes so the bubble and the countdown agree, and never more than
/// JailState.MaxSentenceSeconds. The charges are then dropped - served, not
/// deleted: `dropped_at` is stamped, so the history still reads.
///
/// Uncuffed on the way in, and the pair goes back to the officer whose cuffs
/// they were (PoliceState.ReturnCuffs, which was written for this).
/// </summary>
internal class ArrestCommand : ITargetChatCommand
{
    public string Key => "arrest";

    // Every player, like :ps and :fb: the real gate is being an on-duty
    // officer (PoliceUtility), which the first line below checks.
    public string PermissionRequired => "";

    public string Parameters => "%target%";

    public string Description => "Send a wanted suspect you are escorting to jail, from an arrest point.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "No target selected.";

    /// <summary>Blue bubble, the one every police action shares.</summary>
    private const int PoliceBubble = 4;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        // Police powers are a job: on the force AND clocked in.
        if (!PoliceUtility.RequireOnDuty(session, "arrest someone"))
            return Task.CompletedTask;

        if (target == habbo)
        {
            session.SendWhisper("You cannot arrest yourself.");
            return Task.CompletedTask;
        }

        // Never on one of your own characters - see ChargeCommand.
        if (AccountUtility.SameAccount(habbo.Id, target.Id))
        {
            session.SendWhisper("That is one of your own characters.");
            return Task.CompletedTask;
        }

        var manager = room.GetRoomUserManager();
        var officerUser = manager.GetRoomUserByHabbo(habbo.Id);
        var suspectUser = manager.GetRoomUserByHabbo(target.Id);
        if (officerUser == null)
            return Task.CompletedTask;
        if (suspectUser == null)
        {
            session.SendWhisper($"{target.Username} is not in this room.");
            return Task.CompletedTask;
        }

        if (JailState.IsJailed(target.Id))
        {
            session.SendWhisper($"{target.Username} is already in jail.");
            return Task.CompletedTask;
        }

        // A sheet that has lapsed is nothing to arrest for, so sweep first.
        WantedUtility.ExpireLapsed();
        var (openCharges, jailSeconds) = OpenSheet(target.Id);
        if (openCharges == 0)
        {
            session.SendWhisper($"{target.Username} is not wanted.");
            return Task.CompletedTask;
        }

        if (!PoliceState.IsCuffed(target.Id))
        {
            session.SendWhisper($"{target.Username} needs to be cuffed first.");
            return Task.CompletedTask;
        }

        if (PoliceState.CaptorOf(target.Id) != habbo.Id || PoliceState.IsMedicalEscort(habbo.Id))
        {
            session.SendWhisper($"You need to be escorting {target.Username}.");
            return Task.CompletedTask;
        }

        if (!OnArrestPoint(room, officerUser, suspectUser))
        {
            session.SendWhisper($"You or {target.Username} need to be standing on an arrest point.");
            return Task.CompletedTask;
        }

        if (jailSeconds <= 0)
        {
            session.SendWhisper($"{target.Username}'s charges carry no jail time.");
            return Task.CompletedTask;
        }

        var jailRoomId = JailState.JailRoomId();
        if (jailRoomId == 0)
        {
            session.SendWhisper("There is no jail to send them to.");
            return Task.CompletedTask;
        }

        var minutes = (int)Math.Min(JailState.MaxSentenceSeconds / 60, (jailSeconds + 59) / 60);
        var unit = minutes == 1 ? "minute" : "minutes";

        // Served, so off the sheet.
        DropCharges(target.Id);

        // Out of the officer's hands, and out of the cuffs - read whose cuffs
        // before the uncuff forgets it.
        var cufferId = PoliceState.CufferOf(target.Id);
        PoliceState.EndEscort(room, habbo.Id, suspectUser);
        PoliceState.Uncuff(target.Id);
        if (cufferId != 0 && PoliceState.ReturnCuffs(cufferId) && cufferId != habbo.Id)
            PlusEnvironment.Game.ClientManager.GetClientByUserId(cufferId)?.SendWhisper($"{habbo.Username} booked {target.Username} - your handcuffs are back in your backpack.");

        // Wrapped in "*" so the client narrates it: "*Twist sends Kat to jail
        // for 12 minutes*". The name is never in the text or it prints twice.
        room.SendPacket(new ChatComposer(officerUser.VirtualId,
            $"*sends {target.Username} to jail for {minutes} {unit}*", 0, PoliceBubble));

        // Sentenced BEFORE the move: the jail's door lets a prisoner through
        // even when it is full or locked, and only a prisoner. They land on a
        // bed there (JailState.SendToJail).
        JailState.Start(target, habbo.Id, jailRoomId, minutes * 60);
        target.Client?.SendWhisper($"You have been sent to jail for {minutes} {unit}.");
        JailState.SendToJail(target, jailRoomId, forward: false);

        // They were on the wanted list; they are not now.
        WantedUtility.Broadcast();
        return Task.CompletedTask;
    }

    /// <summary>
    /// The pair has stopped with one of them on an Arrest Point. Stopped means
    /// the officer is not walking - the suspect only moves when their captor
    /// does - and the suspect is not part way through a step: mid-step the two
    /// reach tiles differ (MovementV2Bridge.ReachTiles), and an arrest that
    /// lands a tile early is the thing this refuses. A stopped officer's own
    /// tile is simply where they stand.
    /// </summary>
    private static bool OnArrestPoint(Room room, RoomUser officer, RoomUser suspect)
    {
        if (MovementV2Bridge.IsWalkingOrWaiting(officer))
            return false;
        var (first, second) = MovementV2Bridge.ReachTiles(suspect);
        if (first != second)
            return false;
        return IsArrestPoint(room, officer.X, officer.Y) || IsArrestPoint(room, first.X, first.Y);
    }

    /// <summary>
    /// Whether an arrest_point furni covers this tile. The item's own
    /// Definition, so a behaviour scoped to that one piece (the Furni function
    /// editor's single-item scope) counts as much as one set on its type.
    /// </summary>
    private static bool IsArrestPoint(Room room, int x, int y)
    {
        var items = room.GetGameMap()?.GetAllRoomItemForSquare(x, y);
        if (items == null)
            return false;
        foreach (var item in items)
        {
            if (item?.Definition != null && item.Definition.InteractionType == InteractionType.ArrestPoint)
                return true;
        }
        return false;
    }

    /// <summary>How many charges are open, and their jail time summed in seconds.</summary>
    private static (int Open, long JailSeconds) OpenSheet(int userId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery(
            "SELECT COUNT(*) AS open_count, CAST(COALESCE(SUM(c.`jail_seconds`), 0) AS SIGNED) AS jail " +
            "FROM `rp_charges` ch JOIN `rp_crimes` c ON c.`id` = ch.`crime_id` " +
            "WHERE ch.`user_id` = @user AND ch.`dropped_at` = 0");
        dbClient.AddParameter("user", userId);
        var row = dbClient.GetRow();
        if (row == null)
            return (0, 0);
        return (Convert.ToInt32(row["open_count"]), Convert.ToInt64(row["jail"]));
    }

    /// <summary>Stamp every open charge as dropped - the same write :pardon makes.</summary>
    private static void DropCharges(int userId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `rp_charges` SET `dropped_at` = UNIX_TIMESTAMP() " +
                          "WHERE `user_id` = @user AND `dropped_at` = 0");
        dbClient.AddParameter("user", userId);
        dbClient.RunQuery();
    }
}
