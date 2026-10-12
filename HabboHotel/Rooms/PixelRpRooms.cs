using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.Communication.Packets.Outgoing.Navigator;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// pixelrp: the city's own rooms - where the gameplay is held - belong to the
/// PixelRP account (migration 247), not to a player.
///
/// Staff turn a room into one from the Room Tool (RpMakePixelRpRoomEvent): the
/// room, every furni in it and every bot in it are handed to PixelRP. After
/// that, furni and bots put down in the room become PixelRP's as they land, so
/// it never drifts back to mixed ownership. A staff member who picks up furni
/// or a bot in one gets it as their own - into their inventory and owned by
/// them - never sent off to PixelRP's inventory, where nobody could reach it.
///
/// A room's owner has to be a real account (every room load joins `users` on
/// `rooms`.`owner`), which is why PixelRP is one. Nobody can log in as it, and
/// `users`.`rp_system_account` keeps it out of every search a player can use.
/// </summary>
public static class PixelRpRooms
{
    public const string AccountName = "PixelRP";

    /// <summary>How long a missing account is remembered before the database is asked again.</summary>
    private const long MissingRetryMs = 60_000;

    private static int _accountId;
    private static long _lookedUpAt;

    /// <summary>
    /// The PixelRP account's id - or 0 before migration 247 has run. Found
    /// once and kept; a miss is asked again at most once a minute, since this
    /// is read on every pickup and placement.
    /// </summary>
    public static int AccountId
    {
        get
        {
            if (_accountId > 0)
                return _accountId;
            var now = Environment.TickCount64;
            if (_lookedUpAt != 0 && now - _lookedUpAt < MissingRetryMs)
                return 0;
            _lookedUpAt = now;
            try
            {
                using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
                dbClient.SetQuery("SELECT `id` FROM `users` WHERE `username` = @name AND `rp_system_account` = 1 LIMIT 1");
                dbClient.AddParameter("name", AccountName);
                _accountId = dbClient.GetInteger();
            }
            catch
            {
                _accountId = 0;
            }
            return _accountId;
        }
    }

    public static bool IsPixelRpRoom(RoomData? room) => room != null && room.OwnerId > 0 && room.OwnerId == AccountId;

    public static bool IsStaff(Habbo? habbo) => habbo != null && habbo.Rank >= Room.SettingsStaffRank;

    /// <summary>One furni's owner, in every field that carries it.</summary>
    public static void SetItemOwner(Item item, int ownerId, string ownerName)
    {
        item.OwnerId = (uint)ownerId;
        item.UserId = ownerId;
        item.Username = ownerName;
    }

    /// <summary>
    /// The Room Tool's "Make PixelRP room": the room, its furni and its bots
    /// (not pets) become PixelRP's. One way - there is no button back.
    /// </summary>
    public static void MakePixelRpRoom(GameClient session, Room room)
    {
        var staff = session.GetHabbo();
        var accountId = AccountId;
        if (accountId <= 0)
        {
            session.SendWhisper("The PixelRP account is missing.");
            return;
        }
        if (room.OwnerId == accountId)
        {
            session.SendWhisper("This room already belongs to PixelRP.");
            return;
        }

        var previousOwner = room.OwnerName;
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.SetQuery("UPDATE `rooms` SET `owner` = @owner WHERE `id` = @room LIMIT 1");
            dbClient.AddParameter("owner", accountId);
            dbClient.AddParameter("room", room.Id);
            dbClient.RunQuery();
            dbClient.SetQuery("UPDATE `items` SET `user_id` = @owner WHERE `room_id` = @room");
            dbClient.AddParameter("owner", accountId);
            dbClient.AddParameter("room", room.Id);
            dbClient.RunQuery();
            dbClient.SetQuery("UPDATE `bots` SET `user_id` = @owner WHERE `room_id` = @room AND `ai_type` <> 'pet'");
            dbClient.AddParameter("owner", accountId);
            dbClient.AddParameter("room", room.Id);
            dbClient.RunQuery();
            // The same log the staff chat commands write to.
            dbClient.SetQuery("INSERT INTO `logs_client_staff` (`user_id`,`data_string`,`machine_id`,`timestamp`) VALUES (@user,@data,@machine,@time)");
            dbClient.AddParameter("user", staff.Id);
            dbClient.AddParameter("data", $"Room Tool: made room {room.Id} ({room.Name}) a PixelRP room - it was {previousOwner}'s");
            dbClient.AddParameter("machine", staff.MachineId ?? string.Empty);
            dbClient.AddParameter("time", PlusEnvironment.GetUnixTimestamp());
            dbClient.RunQuery();
        }

        room.OwnerId = accountId;
        room.OwnerName = AccountName;
        foreach (var item in room.GetRoomItemHandler().GetWallAndFloor.ToList())
            SetItemOwner(item, accountId, AccountName);
        var users = room.GetRoomUserManager().GetUserList().ToList();
        foreach (var user in users)
        {
            if (user != null && user.IsBot && !user.IsPet && user.BotData != null)
                user.BotData.OwnerId = accountId;
        }

        // Everybody inside: the old owner's tools go, and the room's info
        // shows its new owner. Furni owner names in the infostand catch up
        // when the room is next loaded.
        foreach (var user in users)
        {
            var client = user?.GetClient();
            if (client?.GetHabbo() != null && !user.IsBot)
                room.GetRoomUserManager().PushRoomRights(client);
        }
        room.SendPacket(new RoomInfoUpdatedComposer(room.Id));
        session.Send(new ModeratorRoomInfoComposer(room, room.GetRoomUserManager().GetRoomUserByHabbo(room.OwnerName) != null));
        session.SendWhisper("This room now belongs to PixelRP.");
    }

    /// <summary>
    /// Furni put down in a PixelRP room becomes PixelRP's, whoever's inventory
    /// it came from. Called where a placement stamps the furni's owner.
    /// </summary>
    public static void ClaimPlacedItem(Room room, Item item)
    {
        if (!IsPixelRpRoom(room) || item.UserId == room.OwnerId)
            return;
        SetItemOwner(item, room.OwnerId, AccountName);
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.SetQuery("UPDATE `items` SET `user_id` = @owner WHERE `id` = @id LIMIT 1");
        dbClient.AddParameter("owner", room.OwnerId);
        dbClient.AddParameter("id", item.Id);
        dbClient.RunQuery();
    }

    /// <summary>
    /// Does this pickup hand the furni or bot to the staff member who took it?
    /// Only in a PixelRP room, only for staff, and only where they may pick
    /// things up at all (on duty, or given rights) - the same reach as
    /// furni, so a bot is no easier to take than a chair.
    /// </summary>
    public static bool StaffTakesIt(Room room, GameClient session) =>
        IsPixelRpRoom(room) && IsStaff(session?.GetHabbo()) && room.CheckRights(session, false);
}
