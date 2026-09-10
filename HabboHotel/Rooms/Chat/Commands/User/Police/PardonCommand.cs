using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User.Police;

/// <summary>
/// pixelrp police actions: :pardon &lt;player&gt; - clear someone's whole rap
/// sheet.
///
/// The other half of :charge, and the only way a sheet ever empties: charges
/// are a record, so nothing lapses on its own. Being WANTED does lapse - the
/// list only holds a player for fifteen minutes after their latest charge -
/// but the charges themselves sit there until an officer drops them.
///
/// All of them, not one: an officer who wants to remove a single count is
/// asking for a rap-sheet view that does not exist yet, and a per-charge
/// command with no way to see the sheet would be guesswork. Nothing is
/// deleted either - `dropped_at` is stamped, so the history still reads.
///
/// No self-pardon. A cop clearing their own record is the one use of this
/// that no police force would allow, and the roleplay is better for making
/// them ask a colleague.
/// </summary>
internal class PardonCommand : ITargetChatCommand
{
    public string Key => "pardon";

    public string PermissionRequired => "command_pardon";

    public string Parameters => "%target%";

    public string Description => "Drop every charge against another user.";

    public bool MustBeInSameRoom => true;

    public string NoTargetMessage => "No target selected.";

    /// <summary>Blue bubble, the one every police action shares.</summary>
    private const int PoliceBubble = 4;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var habbo = session.GetHabbo();
        // Police powers are a job: on the force AND clocked in.
        if (!PoliceUtility.RequireOnDuty(session, "pardon someone"))
            return Task.CompletedTask;

        if (target == habbo)
        {
            session.SendWhisper("You cannot pardon yourself.");
            return Task.CompletedTask;
        }

        // Same rule as :charge - a sheet is cleared in front of the room it
        // was filled in, not from across the hotel.
        if (room.GetRoomUserManager().GetRoomUserByHabbo(target.Id) == null)
        {
            session.SendWhisper($"{target.Username} is not in this room.");
            return Task.CompletedTask;
        }

        // Resolved before the write: an officer with no room unit cannot be
        // announced, and a pardon nobody in the room saw is worse than none.
        var officerUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);
        if (officerUser == null)
            return Task.CompletedTask;

        var dropped = Drop(target.Id, habbo.Id);
        if (dropped == 0)
        {
            session.SendWhisper($"{target.Username} has nothing on their record.");
            return Task.CompletedTask;
        }

        // Leading AND trailing "*" matter: the client only narrates a style-4
        // bubble when the text is wrapped in them, and it then moves the
        // opening marker ahead of the actor's name - so this renders as
        // "*Yavn pardons twist, dropping all charges*". The name is never in
        // the text itself or it would print twice.
        room.SendPacket(new ChatComposer(officerUser.VirtualId,
            $"*pardons {target.Username}, dropping all charges*", 0, PoliceBubble));

        // They may have been on the wanted list; they are not now.
        WantedUtility.Broadcast();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stamp every open charge as dropped and say how many there were.
    /// `officer_id` is left as the officer who FILED each charge - that is
    /// what it records - so who pardoned whom lives in the room log only.
    /// </summary>
    private static int Drop(int userId, int officerId)
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("SELECT COUNT(*) FROM `rp_charges` WHERE `user_id` = @user AND `dropped_at` = 0");
        dbClient.AddParameter("user", userId);
        var open = dbClient.GetInteger();
        if (open == 0)
            return 0;

        dbClient.SetQuery("UPDATE `rp_charges` SET `dropped_at` = UNIX_TIMESTAMP() " +
                          "WHERE `user_id` = @user AND `dropped_at` = 0");
        dbClient.AddParameter("user", userId);
        dbClient.RunQuery();
        return open;
    }
}
