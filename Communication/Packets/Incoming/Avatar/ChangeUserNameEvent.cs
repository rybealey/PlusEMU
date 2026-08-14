using Plus.Communication.Packets.Outgoing.Navigator;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.Database;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.Utilities;
using Dapper;
using Plus.HabboHotel.Users.UserData;

namespace Plus.Communication.Packets.Incoming.Avatar;

internal class ChangeUserNameEvent : IPacketEvent
{
    private readonly IUserDataFactory _userDataFactory;
    private readonly IGameClientManager _clientManager;
    private readonly IRoomManager _roomManager;
    private readonly IAchievementManager _achievementManager;
    private readonly IDatabase _database;

    public ChangeUserNameEvent(IUserDataFactory userDataFactory, IGameClientManager clientManager, IRoomManager roomManager, IAchievementManager achievementManager, IDatabase database)
    {
        _userDataFactory = userDataFactory;
        _clientManager = clientManager;
        _roomManager = roomManager;
        _achievementManager = achievementManager;
        _database = database;
    }

    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var room = session.GetHabbo().CurrentRoom;
        if (room == null)
            return;
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Username);
        if (user == null)
            return;
        var newName = packet.ReadString();
        var oldName = session.GetHabbo().Username;
        if (newName == oldName)
        {
            session.GetHabbo().ChangeName(oldName);
            session.Send(new UpdateUsernameComposer(newName));
            return;
        }
        if (!CanChangeName(session.GetHabbo()))
        {
            session.SendNotification("Oops, it appears you currently cannot change your username!");
            return;
        }
        var inUse = await _userDataFactory.HabboExists(newName);
        if (inUse)
            return;
        var letters = newName.ToLower().ToCharArray();
        const string allowedCharacters = "abcdefghijklmnopqrstuvwxyz.,_-;:?!1234567890";
        if (letters.Any(chr => !allowedCharacters.Contains(chr)))
            return;
        if (!session.GetHabbo().Permissions.HasRight("mod_tool") && newName.ToLower().Contains("mod") || newName.ToLower().Contains("adm") || newName.ToLower().Contains("admin")
            || newName.ToLower().Contains("m0d") || newName.ToLower().Contains("mob") || newName.ToLower().Contains("m0b"))
            return;
        if (!newName.ToLower().Contains("mod") && (session.GetHabbo().Rank == 2 || session.GetHabbo().Rank == 3))
            return;
        if (newName.Length > 15)
            return;
        if (newName.Length < 3)
            return;
        if (!_clientManager.UpdateClientUsername(session, oldName, newName))
        {
            session.SendNotification("Oops! An issue occoured whilst updating your username.");
            return;
        }
        session.GetHabbo().ChangingName = false;
        room.GetRoomUserManager().RemoveUserFromRoom(session, true);
        session.GetHabbo().ChangeName(newName);
        session.GetHabbo().Messenger.NotifyChangesToFriends();
        session.Send(new UpdateUsernameComposer(newName));
        room.SendPacket(new UserNameChangeComposer(room.Id, user.VirtualId, newName));
        using (var connection = _database.Connection())
        {
            connection.Execute("INSERT INTO `logs_client_namechange` (`user_id`,`new_name`,`old_name`,`timestamp`) VALUES (@id,@new_name,@old_name,@timestamp)",
                    new { id = session.GetHabbo().Id, new_name = newName, old_name = oldName, timestamp = UnixTimestamp.GetNow() });
        }
        foreach (var ownRooms in _roomManager.GetRooms().ToList())
        {
            if (ownRooms == null || ownRooms.OwnerId != session.GetHabbo().Id || ownRooms.OwnerName == newName)
                continue;
            ownRooms.OwnerName = newName;
            ownRooms.SendPacket(new RoomInfoUpdatedComposer(ownRooms.Id));
        }
        _achievementManager.ProgressAchievement(session, "ACH_Name", 1);
        session.SendRoomForward(room.Id);
        return;
    }

    private static bool CanChangeName(Habbo habbo)
    {
        if (habbo.Rank == 1 && habbo.VipRank == 0 && habbo.LastNameChange == 0)
            return true;
        if (habbo.Rank == 1 && habbo.VipRank == 1 && (habbo.LastNameChange == 0 || UnixTimestamp.GetNow() + 604800 > habbo.LastNameChange))
            return true;
        if (habbo.Rank == 1 && habbo.VipRank == 2 && (habbo.LastNameChange == 0 || UnixTimestamp.GetNow() + 86400 > habbo.LastNameChange))
            return true;
        if (habbo.Rank == 1 && habbo.VipRank == 3)
            return true;
        if (habbo.Permissions.HasRight("mod_tool"))
            return true;
        return false;
    }
}