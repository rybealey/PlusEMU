using System.Drawing;
using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Permissions;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.Core;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.Trading;
using Plus.Utilities;

using Dapper;

namespace Plus.HabboHotel.Rooms;

public class RoomUserManager
{
    private ConcurrentDictionary<int, RoomUser> _bots;
    private ConcurrentDictionary<int, RoomUser> _pets;

    private int _primaryPrivateUserId;
    private Room _room;
    private int _secondaryPrivateUserId;
    private ConcurrentDictionary<int, RoomUser> _users;

    // pixelrp instant-first-step: serializes TryInstantFirstStep against OnCycle's tick.
    private readonly object _cycleLock = new();

    /// <summary>
    /// How many 500ms ticks between position-persistence checks for a user whose tile changed.
    /// Four ticks (~2s) keeps the stored position close enough that a crash costs at most a
    /// couple of steps, without writing on every footfall.
    /// </summary>
    private const int PositionSaveIntervalCycles = 4;

    public int UserCount;


    public RoomUserManager(Room room)
    {
        _room = room;
        _users = new();
        _pets = new();
        _bots = new();
        _primaryPrivateUserId = 0;
        _secondaryPrivateUserId = 0;
        PetCount = 0;
        UserCount = 0;
    }

    public int PetCount { get; private set; }

    public RoomUser DeployBot(RoomBot bot, Pet pet)
    {
        var user = new RoomUser(0, _room.RoomId, _primaryPrivateUserId++, _room);
        // Match the avatar's virtual id. Using _primaryPrivateUserId here read
        // the ALREADY-incremented counter, so BotData.VirtualId was one ahead of
        // the bot's own avatar (and collided with the next avatar deployed) —
        // which is why "Copy my looks" (UserChangeComposer(BotData)) targeted the
        // wrong/missing avatar and never applied.
        bot.VirtualId = user.VirtualId;
        var personalId = _secondaryPrivateUserId++;
        user.InternalRoomId = personalId;
        _users.TryAdd(personalId, user);
        var model = _room.GetGameMap().Model;
        if (bot.X > 0 && bot.Y > 0 && bot.X < model.MapSizeX && bot.Y < model.MapSizeY)
        {
            user.SetPos(bot.X, bot.Y, bot.Z);
            user.SetRot(bot.Rot, false);
        }
        else
        {
            bot.X = model.DoorX;
            bot.Y = model.DoorY;
            user.SetPos(model.DoorX, model.DoorY, model.DoorZ);
            user.SetRot(model.DoorOrientation, false);
        }
        user.BotData = bot;
        user.BotAi = bot.GenerateBotAi(user.VirtualId);
        if (user.IsPet)
        {
            user.BotAi.Init(bot.BotId, user.VirtualId, _room.RoomId, user, _room);
            user.PetData = pet;
            user.PetData.VirtualId = user.VirtualId;
        }
        else
            user.BotAi.Init(bot.BotId, user.VirtualId, _room.RoomId, user, _room);
        user.UpdateNeeded = true;
        _room.SendPacket(new UsersComposer(user));
        if (user.IsPet)
        {
            if (_pets.ContainsKey(user.PetData.PetId))
                _pets[user.PetData.PetId] = user;
            else
                _pets.TryAdd(user.PetData.PetId, user);
            PetCount++;
        }
        else if (user.IsBot)
        {
            if (_bots.ContainsKey(user.BotData.BotId))
                _bots[user.BotData.BotId] = user;
            else
                _bots.TryAdd(user.BotData.Id, user);
            _room.SendPacket(new DanceComposer(user, user.BotData.DanceId));
            // Identify bots on the map — every bot wears the identifier effect.
            _room.SendPacket(new AvatarEffectComposer(user.VirtualId, RoomBot.IdentifierEffect));
        }
        return user;
    }

    public void RemoveBot(int virtualId, bool kicked)
    {
        var user = GetRoomUserByVirtualId(virtualId);
        if (user == null || !user.IsBot)
            return;
        if (user.IsPet)
        {
            _pets.TryRemove(user.PetData.PetId, out var pet);
            PetCount--;
        }
        else
            _bots.TryRemove(user.BotData.Id, out var bot);
        user.BotAi.OnSelfLeaveRoom(kicked);
        _room.SendPacket(new UserRemoveComposer(user.VirtualId));
        if (_users != null)
            _users.TryRemove(user.InternalRoomId, out var toRemove);
        OnRemove(user);
    }

    public RoomUser GetUserForSquare(int x, int y) => _room.GetGameMap().GetRoomUsers(new(x, y)).FirstOrDefault();

    // pixelrp RP stats: apply/lift the 0-health knockout state and push the
    // resulting posture to the room immediately (commands run off-tick, so
    // without this the lay/stand would wait for the next 500ms cycle).
    public void ApplyRpKnockout(RoomUser user)
    {
        if (user == null)
            return;
        lock (_cycleLock)
        {
            user.UpdateRpKnockoutState();
            SerializeStatusUpdates();
        }
    }

    public bool AddAvatarToRoom(GameClient session)
    {
        if (_room == null)
            return false;
        if (session == null)
            return false;
        if (session.GetHabbo().CurrentRoom == null)
            return false;
        if (_users.Any(u => u.Value.UserId == session.GetHabbo().Id))
            return false;
        var user = new RoomUser(session.GetHabbo().Id, _room.RoomId, _primaryPrivateUserId++, _room);
        if (user == null || user.GetClient() == null)
            return false;
        user.UserId = session.GetHabbo().Id;
        session.GetHabbo().TentId = 0;
        var personalId = _secondaryPrivateUserId++;
        user.InternalRoomId = personalId;
        session.GetHabbo().CurrentRoom = _room;
        if (!_users.TryAdd(personalId, user))
            return false;
        var model = _room.GetGameMap().Model;
        if (model == null)
            return false;
        if (!_room.PetMorphsAllowed && session.GetHabbo().PetId != 0)
            session.GetHabbo().PetId = 0;
        if (!session.GetHabbo().IsTeleporting && !session.GetHabbo().IsHopping)
        {
            if (!model.DoorIsValid())
            {
                var square = _room.GetGameMap().GetRandomWalkableSquare();
                model.DoorX = square.X;
                model.DoorY = square.Y;
                model.DoorZ = (int)_room.GetGameMap().GetHeightForSquareFromData(square);
            }
            // pixelrp last-position restore: if this entry is the login forward to the
            // user's last room, spawn on the saved tile/rotation instead of the door.
            // Furni-blocked/occupied Open squares are allowed (exact continuity); a tile
            // outside the current model, or a Blocked map square (room remodeled into a
            // wall), falls back to the door. The marker is single-use: cleared below on
            // ANY room entry.
            var restore = session.GetHabbo().PendingRestore;
            if (restore != null && restore.IsFresh && restore.RoomId == _room.RoomId
                && restore.X >= 0 && restore.X < model.MapSizeX
                && restore.Y >= 0 && restore.Y < model.MapSizeY
                && model.SqState[restore.X, restore.Y] != SquareState.Blocked)
            {
                user.SetPos(restore.X, restore.Y, _room.GetGameMap().SqAbsoluteHeight(restore.X, restore.Y));
                // Bypass SetRot's sit/head-turn heuristic (it can leave RotBody at 0 for
                // some diffs, e.g. rotation 1) - assign body/head rotation directly,
                // clamped since last_rot is an unbounded signed int in the schema.
                var rot = ((restore.Rot % 8) + 8) % 8;
                user.RotBody = rot;
                user.RotHead = rot;
            }
            else
            {
                user.SetPos(model.DoorX, model.DoorY, model.DoorZ);
                user.SetRot(model.DoorOrientation, false);
            }
            session.GetHabbo().PendingRestore = null;
        }
        else if (!user.IsBot && (user.GetClient().GetHabbo().IsTeleporting || user.GetClient().GetHabbo().IsHopping))
        {
            Item item = null;
            if (session.GetHabbo().IsTeleporting)
                item = _room.GetRoomItemHandler().GetItem(session.GetHabbo().TeleporterId);
            else if (session.GetHabbo().IsHopping)
                item = _room.GetRoomItemHandler().GetItem(session.GetHabbo().HopperId);
            if (item != null)
            {
                if (session.GetHabbo().IsTeleporting)
                {
                    item.LegacyDataString = "2";
                    item.UpdateState(false, true);
                    user.SetPos(item.GetX, item.GetY, item.GetZ);
                    user.SetRot(item.Rotation, false);
                    item.InteractingUser2 = session.GetHabbo().Id;
                    item.LegacyDataString = "0";
                    item.UpdateState(false, true);
                }
                else if (session.GetHabbo().IsHopping)
                {
                    item.LegacyDataString = "1";
                    item.UpdateState(false, true);
                    user.SetPos(item.GetX, item.GetY, item.GetZ);
                    user.SetRot(item.Rotation, false);
                    user.AllowOverride = false;
                    item.InteractingUser2 = session.GetHabbo().Id;
                    item.LegacyDataString = "2";
                    item.UpdateState(false, true);
                }
            }
            else
            {
                user.SetPos(model.DoorX, model.DoorY, model.DoorZ - 1);
                user.SetRot(model.DoorOrientation, false);
            }
            // pixelrp last-position restore: a user who logs out inside a teleporter
            // re-enters via teleport, not the door branch above; the marker must
            // still be consumed here so it can't leak into a later manual entry.
            session.GetHabbo().PendingRestore = null;
        }
        _room.SendPacket(new UsersComposer(user));
        // pixelrp RP stats: announce the entering player's health/energy to the
        // room (the enterer receives everyone else's in Room.SendObjects).
        session.GetHabbo().EnsureRpStatsLoaded();
        _room.SendPacket(new RpStatsComposer(user.VirtualId, session.GetHabbo().RpHealth, session.GetHabbo().RpHealthMax, session.GetHabbo().RpEnergy, session.GetHabbo().RpEnergyMax, (int)Math.Round(session.GetHabbo().RpAggression), session.GetHabbo().RpPassiveSeconds > 0 ? 1 : 0, session.GetHabbo().Rank >= 5 ? 1 : 0));
        // pixelrp corporations: announce the entering player's employment.
        var enteringEmployment = Plus.HabboHotel.Corporations.CorporationUtility.GetEmployment(session.GetHabbo().Id);
        if (enteringEmployment != null)
            _room.SendPacket(Plus.HabboHotel.Corporations.CorporationUtility.ComposeFor(session.GetHabbo().Id, enteringEmployment));
        // Knocked-out players (0 health persists) re-enter laying and frozen.
        user.UpdateRpKnockoutState();
        if (_room.CheckRights(session, true))
        {
            user.SetStatus("flatctrl", "useradmin");
            session.Send(new YouAreOwnerComposer());
            // Nitro only shows the branding (ads_background) editor at controller
            // level 5 ("moderator"); level 4 caps out at plain owner tools.
            session.Send(new YouAreControllerComposer(
                session.GetHabbo().Permissions.HasRight("room_item_save_branding_items") ? 5 : 4));
        }
        else if (_room.CheckRights(session, false) && _room.Group == null)
        {
            user.SetStatus("flatctrl", "1");
            session.Send(new YouAreControllerComposer(1));
        }
        else if (_room.Group != null && _room.CheckRights(session, false, true))
        {
            user.SetStatus("flatctrl", "3");
            session.Send(new YouAreControllerComposer(3));
        }
        else
            session.Send(new YouAreNotControllerComposer());
        user.UpdateNeeded = true;
        // Staff are no longer given a forced effect (102) on room entry.
        if (session.GetHabbo().IsAmbassador && !session.GetHabbo().DisableForcedEffects && !session.GetHabbo().Permissions.HasRight("mod_tool"))
            session.GetHabbo().Effects.ApplyEffect(178);
        foreach (var bot in _bots.Values.ToList())
        {
            if (bot == null || bot.BotAi == null)
                continue;
            bot.BotAi.OnUserEnterRoom(user);
        }
        return true;
    }

    public void RemoveUserFromRoom(GameClient session, bool nofityUser, bool notifyKick = false)
    {
        try
        {
            if (_room == null)
                return;
            if (session == null || session.GetHabbo() == null)
                return;
            if (notifyKick)
                session.Send(new GenericErrorComposer(4008));
            if (nofityUser)
                session.Send(new CloseConnectionComposer());
            if (session.GetHabbo().TentId > 0)
                session.GetHabbo().TentId = 0;
            session.GetHabbo().CurrentRoom = null;
            var user = GetRoomUserByHabbo(session.GetHabbo().Id);
            if (user != null)
            {
                // pixelrp last-position restore: capture where the user stood before the
                // room state is torn down; persisted below alongside the roomvisit update.
                var lastX = user.X;
                var lastY = user.Y;
                var lastRot = user.RotBody;
                if (user.RidingHorse)
                {
                    user.RidingHorse = false;
                    var userRiding = GetRoomUserByVirtualId(user.HorseId);
                    if (userRiding != null)
                    {
                        userRiding.RidingHorse = false;
                        userRiding.HorseId = 0;
                    }
                }
                if (user.Team != Team.None)
                {
                    var team = _room.GetTeamManagerForFreeze();
                    if (team != null)
                    {
                        team.OnUserLeave(user);
                        user.Team = Team.None;
                        if (user.GetClient().GetHabbo().Effects.CurrentEffect != 0)
                            user.GetClient().GetHabbo().Effects.ApplyEffect(0);
                    }
                }
                // Dressing booth: the avatar editor is global client UI and would
                // survive the room change; close it and reopen the curtain.
                foreach (var tileItem in _room.GetGameMap().GetCoordinatedItems(new(user.X, user.Y)))
                {
                    if (tileItem.Definition.InteractionType != InteractionType.DressingBooth)
                        continue;
                    tileItem.LegacyDataString = "0";
                    tileItem.UpdateState(false, true);
                    session.Send(new InClientLinkComposer("avatar-editor/hide"));
                }
                RemoveRoomUser(user);
                if (user.CurrentItemEffect != ItemEffectType.None)
                {
                    if (session.GetHabbo().Effects != null)
                        session.GetHabbo().Effects.CurrentEffect = -1;
                }
                if (user.IsTrading)
                {
                    Trade trade = null;
                    if (_room.GetTrading().TryGetTrade(user.TradeId, out trade))
                        trade.EndTrade(user.TradeId);
                }

                //Session.GetHabbo().CurrentRoomId = 0;
                    session.GetHabbo().Messenger?.NotifyChangesToFriends();
                using (var dbClient = PlusEnvironment.DatabaseManager.Connection())
                {
                    dbClient.Execute("UPDATE user_roomvisits SET exit_timestamp = @exitTimestamp WHERE room_id = @roomId AND user_id = @userId ORDER BY exit_timestamp DESC LIMIT 1",
                        new
                        {
                            userId = session.GetHabbo().Id,
                            roomId = _room.RoomId,
                            exitTimestamp = UnixTimestamp.GetNow(),
                        });

                    dbClient.Execute("UPDATE `rooms` SET `users_now` = @usersNow WHERE `id` = @roomId LIMIT 1",
                        new
                        {
                            usersNow = _room.UsersNow,
                            roomId = _room.RoomId
                        });

                    // Keep the home room in sync with the last room the user was in, so
                    // the "Home" button and the login restore always agree (they are
                    // otherwise independent). New users - who have never left a room -
                    // keep home_room = 0 and default-spawn into room 1 (Moody's Pointe).
                    dbClient.Execute(
                        "UPDATE `users` SET `last_room_id` = @roomId, `last_x` = @x, `last_y` = @y, `last_rot` = @rot, `home_room` = @roomId WHERE `id` = @userId LIMIT 1",
                        new
                        {
                            userId = session.GetHabbo().Id,
                            roomId = _room.RoomId,
                            x = lastX,
                            y = lastY,
                            rot = lastRot
                        });
                }
                // Mirror it in memory so the logout save (which writes HomeRoom) does not
                // clobber it with the stale value.
                session.GetHabbo().HomeRoom = _room.RoomId;
                if (user != null)
                    user.Dispose();
            }
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    private void OnRemove(RoomUser user)
    {
        try
        {
            var session = user.GetClient();
            if (session == null)
                return;
            var bots = new List<RoomUser>();
            try
            {
                foreach (var roomUser in GetUserList().ToList())
                {
                    if (roomUser == null)
                        continue;
                    if (roomUser.IsBot && !roomUser.IsPet)
                    {
                        if (!bots.Contains(roomUser))
                            bots.Add(roomUser);
                    }
                }
            }
            catch { }
            var petsToRemove = new List<RoomUser>();
            foreach (var bot in bots.ToList())
            {
                if (bot == null || bot.BotAi == null)
                    continue;
                bot.BotAi.OnUserLeaveRoom(session);
                if (bot.IsPet && bot.PetData.OwnerId == user.UserId && !_room.CheckRights(session, true))
                {
                    if (!petsToRemove.Contains(bot))
                        petsToRemove.Add(bot);
                }
            }
            foreach (var toRemove in petsToRemove.ToList())
            {
                if (toRemove == null)
                    continue;
                if (user.GetClient() == null || user.GetClient().GetHabbo() == null || user.GetClient().GetHabbo().Inventory == null)
                    continue;
                if (user.GetClient().GetHabbo().Inventory.Pets.AddPet(toRemove.PetData))
                {
                    toRemove.PetData.RoomId = 0;
                    toRemove.PetData.PlacedInRoom = false;
                    RemoveBot(toRemove.VirtualId, false);
                }
            }
            _room.GetGameMap().RemoveUserFromMap(user, new(user.X, user.Y));
        }
        catch (Exception e)
        {
            ExceptionLogger.LogCriticalException(e);
        }
    }

    private void RemoveRoomUser(RoomUser user)
    {
        // Only restore tile state on leave when room blocking is on. With pixelrp's
        // global tile-overlap (RoomBlockingEnabled always true) users never write
        // occupancy into the pathfinding map, so there is nothing to restore - and
        // SqState is no longer backed up during movement, meaning this write would
        // stamp a stale/default 0 ("blocked") onto the tile. That was the corruption
        // that permanently blocked tiles after users left, making same-tile standing
        // stop working some time after each restart.
        if (!_room.RoomBlockingEnabled)
        {
            if (user.SetStep)
                _room.GetGameMap().GameMap[user.SetX, user.SetY] = user.SqState;
            else
                _room.GetGameMap().GameMap[user.X, user.Y] = user.SqState;
        }
        _room.GetGameMap().RemoveUserFromMap(user, new(user.X, user.Y));
        _room.SendPacket(new UserRemoveComposer(user.VirtualId));
        RoomUser toRemove = null;
        if (_users.TryRemove(user.InternalRoomId, out toRemove))
        {
            //uhmm, could put the below stuff in but idk.
        }
        user.InternalRoomId = -1;
        OnRemove(user);
    }

    public bool TryGetPet(int petId, out RoomUser pet) => _pets.TryGetValue(petId, out pet);

    public bool TryGetBot(int botId, out RoomUser bot) => _bots.TryGetValue(botId, out bot);

    public RoomUser GetBotByName(string name)
    {
        var foundBot = _bots.Count(x => x.Value.BotData != null && x.Value.BotData.Name.ToLower() == name.ToLower()) > 0;
        if (foundBot)
        {
            var id = _bots.FirstOrDefault(x => x.Value.BotData != null && x.Value.BotData.Name.ToLower() == name.ToLower()).Value.BotData.Id;
            return _bots[id];
        }
        return null;
    }

    public void UpdateUserCount(int count)
    {
        UserCount = count;
        _room.UsersNow = count;
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        dbClient.RunQuery($"UPDATE `rooms` SET `users_now` = '{count}' WHERE `id` = '{_room.RoomId}' LIMIT 1");
    }

    public RoomUser GetRoomUserByVirtualId(int virtualId)
    {
        RoomUser user = null;
        if (!_users.TryGetValue(virtualId, out user))
            return null;
        return user;
    }

    public RoomUser GetRoomUserByHabbo(int id)
    {
        var user = GetUserList().Where(x => x != null && x.GetClient() != null && x.GetClient().GetHabbo() != null && x.GetClient().GetHabbo().Id == id).FirstOrDefault();
        if (user != null)
            return user;
        return null;
    }

    public List<RoomUser> GetRoomUsers()
    {
        var list = new List<RoomUser>();
        list = GetUserList().Where(x => !x.IsBot).ToList();
        return list;
    }

    public List<RoomUser> GetRoomUserByRank(int minRank)
    {
        var returnList = new List<RoomUser>();
        foreach (var user in GetUserList().ToList())
        {
            if (user == null)
                continue;
            if (!user.IsBot && user.GetClient() != null && user.GetClient().GetHabbo() != null && user.GetClient().GetHabbo().Rank >= minRank)
                returnList.Add(user);
        }
        return returnList;
    }

    public RoomUser GetRoomUserByHabbo(string pName)
    {
        var user = GetUserList().FirstOrDefault(x =>
            x != null && x.GetClient() != null && x.GetClient().GetHabbo() != null && x.GetClient().GetHabbo().Username.Equals(pName, StringComparison.OrdinalIgnoreCase));
        if (user != null)
            return user;
        return null;
    }

    public void UpdatePets()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        foreach (var pet in GetPets().ToList())
        {
            if (pet == null)
                continue;
            if (pet.DbState == PetDatabaseUpdateState.NeedsInsert)
            {
                dbClient.SetQuery($"INSERT INTO `bots` (`id`,`user_id`,`room_id`,`name`,`x`,`y`,`z`) VALUES ('{pet.PetId}','{pet.OwnerId}','{pet.RoomId}',@name,'0','0','0')");
                dbClient.AddParameter("name", pet.Name);
                dbClient.RunQuery();
                dbClient.SetQuery(
                    $"INSERT INTO `bots_petdata` (`type`,`race`,`color`,`experience`,`energy`,`createstamp`,`nutrition`,`respect`) VALUES ('{pet.Type}',@race,@color,'0','100','{pet.CreationStamp}','0','0')");
                dbClient.AddParameter($"{pet.PetId}race", pet.Race);
                dbClient.AddParameter($"{pet.PetId}color", pet.Color);
                dbClient.RunQuery();
            }
            else if (pet.DbState == PetDatabaseUpdateState.NeedsUpdate)
            {
                //Surely this can be *99 better? // TODO
                var user = GetRoomUserByVirtualId(pet.VirtualId);
                dbClient.RunQuery($"UPDATE `bots` SET room_id = {pet.RoomId}, x = {(user?.X ?? 0)}, Y = {(user?.Y ?? 0)}, Z = {(user?.Z ?? 0)} WHERE `id` = '{pet.PetId}' LIMIT 1");
                dbClient.RunQuery(
                    $"UPDATE `bots_petdata` SET `experience` = '{pet.Experience}', `energy` = '{pet.Energy}', `nutrition` = '{pet.Nutrition}', `respect` = '{pet.Respect}' WHERE `id` = '{pet.PetId}' LIMIT 1");
            }
            pet.DbState = PetDatabaseUpdateState.Updated;
        }
    }

    private void UpdateBots()
    {
        using var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor();
        foreach (var user in GetRoomUsers().ToList())
        {
            if (user == null || !user.IsBot)
                continue;
            if (user.IsBot)
            {
                dbClient.SetQuery("UPDATE bots SET x=@x, y=@y, z=@z, name=@name, look=@look, rotation=@rotation WHERE id=@id LIMIT 1;");
                dbClient.AddParameter("name", user.BotData.Name);
                dbClient.AddParameter("look", user.BotData.Look);
                dbClient.AddParameter("rotation", user.BotData.Rot);
                dbClient.AddParameter("x", user.X);
                dbClient.AddParameter("y", user.Y);
                dbClient.AddParameter("z", user.Z);
                dbClient.AddParameter("id", user.BotData.BotId);
                dbClient.RunQuery();
            }
        }
    }


    public List<Pet> GetPets()
    {
        var pets = new List<Pet>();
        foreach (var user in _pets.Values.ToList())
        {
            if (user == null || !user.IsPet)
                continue;
            pets.Add(user.PetData);
        }
        return pets;
    }

    public void SerializeStatusUpdates()
    {
        var users = new List<RoomUser>();
        var roomUsers = GetUserList();
        if (roomUsers == null)
            return;
        foreach (var user in roomUsers.ToList())
        {
            if (user == null || !user.UpdateNeeded || users.Contains(user))
                continue;
            user.UpdateNeeded = false;
            users.Add(user);
        }
        if (users.Count > 0)
        {
            _room.SendPacket(new UserUpdateComposer(users));

            // pixelrp movement authority: follow each freshly emitted step's
            // "mv" status with its authoritative cycle timing. Seq-gated so a
            // non-step status refresh never re-sends stale timing.
            foreach (var user in users)
            {
                if (user.IsPet || !user.SetStep || !user.IsWalking) continue;
                if (user.MovementSeq == user.MovementSeqSent) continue;
                user.MovementSeqSent = user.MovementSeq;
                _room.SendPacket(new RpMovementCycleComposer(user));
            }
        }
    }

    public void UpdateUserStatusses()
    {
        foreach (var user in GetUserList().ToList())
        {
            if (user == null)
                continue;
            UpdateUserStatus(user, false);
        }
    }

    private bool IsValid(RoomUser user)
    {
        if (user == null)
            return false;
        if (user.IsBot)
            return true;
        if (user.GetClient() == null)
            return false;
        if (user.GetClient().GetHabbo() == null)
            return false;
        if (user.GetClient().GetHabbo().CurrentRoom != _room)
            return false;
        return true;
    }

    // pixelrp instant-first-step: emit a standing unit's first walk step the
    // instant the click arrives instead of waiting up to 500ms for the next
    // room tick. Subsequent steps stay on a per-user metronome beat, so walk
    // SPEED is unchanged. Serialized against the tick via _cycleLock. Kill
    // switch: server_settings key `pathfinder.instant.first.step.disabled` = 1.
    // A click inside the 450ms cooldown no longer falls back to the (late)
    // global tick — it is scheduled at exactly lastStep + 500ms instead, so
    // responsiveness stays consistent while the speed cap holds.
    public void TryInstantFirstStep(RoomUser user)
    {
        if (PlusEnvironment.SettingsManager.TryGetValue("pathfinder.instant.first.step.disabled") == "1") return;
        if (user == null || user.IsBot) return;
        lock (_cycleLock)
        {
            if (!IsValid(user)) return;
            if (user.IsWalking || user.SetStep)
            {
                if (user.PathRecalcNeeded)
                {
                    // Redirect while moving: first try formation admission at
                    // the redirect's natural edge boundary - this is where
                    // real following actually happens (spam-clicks mid-walk).
                    if (TryReserveRedirectFormation(user)) return;

                    // Otherwise the existing beat's ProcessUserMovement
                    // consumes PathRecalcNeeded seamlessly on the own grid.
                    Console.WriteLine($"[ROOMAUTH_SRV] user={user.VirtualId} event=redirect-owned-by-beat seq={user.MovementSeq}");
                }
                return;
            }
            if (!user.PathRecalcNeeded) return;              // no pending request
            var sinceLastMs = (DateTime.Now - user.LastInstantStep).TotalMilliseconds;
            if (sinceLastMs < 450)
            {
                // Rate-capped: schedule the first step at exactly lastStep + 500ms
                // via the self-pace loop instead of leaving it to the global tick.
                if (!user.SelfPaced)
                {
                    user.SelfPaced = true;
                    user.WalkGeneration++;
                    Console.WriteLine($"[ROOMAUTH_FORMATION_REJECT] user={user.VirtualId} kind=fresh reason=rate-capped");
                    Console.WriteLine($"[ROOMAUTH_SRV] user={user.VirtualId} event=rate-capped-prewalk delayMs={Math.Max(1, 500 - (int)sinceLastMs)} seq={user.MovementSeq} gen={user.WalkGeneration}");
                    _ = SelfPaceWalk(user, Math.Max(1, 500 - (int)sinceLastMs), true, user.WalkGeneration);
                }
                return;
            }

            // (Removed 2026-08-22: the "formation join" grid wait — starting a
            // walk near an already-walking player used to hold the first step
            // for up to 500ms to phase-lock the pair from step one. The
            // client's moving-vs-moving magnet blend now smooths that overlap
            // visually, so every start gets the instant first step; the
            // shared-grid slew below still phase-locks walkers within a few
            // beats.)

            // pixelrp formation admission: if a compatible walker's beat
            // boundary lands within the small admission window, anchor this
            // walk to THAT grid instead of the click instant - the first
            // synchronized edge is then already exact BEHIND/ALIGNED/AHEAD
            // with zero correction. Otherwise fall through to the instant
            // start and remain independent (never a fractional formation).
            if (TryReserveFormationEntry(user)) return;

            var throwaway = new List<RoomUser>();
            // The instant step's timing is exact (emitted right now), so it is
            // fully renderable on the room timeline - the local click starts
            // moving with zero authority gap.
            user.NextStepScheduledTick = Environment.TickCount64;
            user.NextStepOnGrid = true;
            ProcessUserMovement(user, throwaway, out _);      // pathfind + emit first step
            user.LastInstantStep = DateTime.Now;
            SerializeStatusUpdates();                          // push the "mv" status now
            if (user.IsWalking)
            {
                // Start a fresh beat anchored to THIS emission. Bumping the
                // generation supersedes any pending loop (e.g. one scheduled by
                // a rate-capped click moments ago) so it exits instead of
                // double-stepping ~30ms after this step.
                user.SelfPaced = true;
                user.WalkGeneration++;
                Console.WriteLine($"[ROOMAUTH_SRV] user={user.VirtualId} event=instant-first-step seq={user.MovementSeq} gen={user.WalkGeneration} tick={Environment.TickCount64}");
                _ = SelfPaceWalk(user, 500, false, user.WalkGeneration);
            }
        }
    }

    // pixelrp formation admission (FORMATION_PRE_ENTRY): decide a clean
    // formation entry BEFORE the joiner's first visible edge exists. If a
    // nearby compatible walker's next beat boundary lands within the small
    // admission window, the joiner's ENTIRE walk is anchored to that walker's
    // 500ms grid by scheduling its first step at that exact boundary - the
    // first synchronized edge is then already the correct real route slot
    // (BEHIND = reference's current edge, ALIGNED = its next, AHEAD = the one
    // after) at the shared phase, with zero position correction, zero speed
    // change and zero mid-walk holds. A click outside the window stays fully
    // independent (the fractional in-between state is never exposed as
    // synchronized). This is scheduling metadata only: nothing here moves,
    // pauses or corrects an avatar, and members simply sharing one anchor is
    // what extends the model to any group size with no pair state.
    // Kill/tune: server_settings `pathfinder.formation.window.ms`
    // (default 120; 0 disables admission entirely).
    private bool TryReserveFormationEntry(RoomUser user)
    {
        // SettingsManager.TryGetValue returns the STRING "0" for MISSING keys
        // (found via the gate trace: every click rejected window-disabled), so
        // 0 cannot mean "off" here. Missing/0 -> default; explicit negative
        // value disables admission.
        //
        // Default 500 = DETERMINISTIC admission: any route-compatible fresh
        // click anchors to the reference grid (start delayed to its next
        // boundary, <=499ms, avg ~250ms). Field data proved the anchor gap
        // between two walks is CONSTANT for their lifetime - redirect retries
        // re-roll nothing, and probabilistic windows (120 -> 24%/walk) simply
        // lose (observed 146/199/242ms misses). The route-relation gate keeps
        // this safe: the delay only ever applies to genuine formation joins
        // (standing exactly on the reference's trail with a matching first
        // edge); ordinary clicks stay instant.
        var windowMs = 500;
        if (int.TryParse(PlusEnvironment.SettingsManager.TryGetValue("pathfinder.formation.window.ms"), out var configured) && configured != 0)
            windowMs = configured;
        if (windowMs < 0)
        {
            Console.WriteLine($"[ROOMAUTH_FORMATION_REJECT] user={user.VirtualId} kind=fresh reason=window-disabled");
            return false;
        }

        var now = Environment.TickCount64;
        var rejectReason = (string)null;
        var humansNearby = 0;
        var gateNotes = new List<string>();

        foreach (var reference in GetUserList().ToList())
        {
            if (reference == null || reference == user || reference.IsBot || reference.IsPet) continue;
            if (Math.Max(Math.Abs(reference.X - user.X), Math.Abs(reference.Y - user.Y)) > 3) continue;
            humansNearby++;
            if (!reference.SelfPaced || !reference.IsWalking || !reference.SetStep)
            {
                gateNotes.Add($"{reference.VirtualId}:not-walking(sp={reference.SelfPaced},w={reference.IsWalking},ss={reference.SetStep})");
                rejectReason ??= "ref-not-walking";
                continue;
            }

            var delay = reference.NextBeatTick - now;
            if (delay <= 0)
            {
                gateNotes.Add($"{reference.VirtualId}:beat-stale({delay})");
                rejectReason ??= "ref-beat-stale";
                continue;
            }
            if (delay > windowMs)
            {
                gateNotes.Add($"{reference.VirtualId}:window({delay}ms)");
                rejectReason ??= $"window-exceeded delayMs={delay}";
                continue;
            }

            // Preview the joiner's REAL first edge (same pathfinder call the
            // walk itself will use at the entry beat).
            var preview = PathFinder.FindPath(user, _room.GetGameMap().DiagonalEnabled, _room.GetGameMap(),
                new(user.X, user.Y), new(user.GoalX, user.GoalY));
            if (preview == null || preview.Count <= 1)
            {
                gateNotes.Add($"{reference.VirtualId}:no-route");
                rejectReason ??= "no-route";
                break;
            }
            var firstTo = preview[preview.Count - 2];

            // Relation from REAL route slots at the entry beat: when the joiner
            // starts, the reference rolls onto its lookahead-1 edge. Matching
            // the reference's CURRENT edge therefore means one slot behind;
            // lookahead-1 means aligned; lookahead-2 means one slot ahead.
            var relation = 99;
            if (user.X == reference.X && user.Y == reference.Y && firstTo.X == reference.SetX && firstTo.Y == reference.SetY)
                relation = -1; // BEHIND: joiner walks the edge the reference is leaving
            else if (reference.LookaheadCount >= 1 && user.X == reference.SetX && user.Y == reference.SetY
                && firstTo.X == reference.Look1X && firstTo.Y == reference.Look1Y)
            {
                // ALIGNED = walking the exact same tiles as the reference:
                // two real players fully merged, with front/behind falling to
                // the renderer's arbitrary same-coordinate tiebreaker (the
                // "he rendered behind me" report). Suppressed by default -
                // auto-admission produces BEHIND/AHEAD only. Enable with
                // server_settings pathfinder.formation.allow.aligned = 1.
                if (PlusEnvironment.SettingsManager.TryGetValue("pathfinder.formation.allow.aligned") != "1")
                {
                    gateNotes.Add($"{reference.VirtualId}:aligned-suppressed");
                    rejectReason ??= "aligned-overlap-suppressed";
                    continue;
                }
                relation = 0;
            }
            else if (reference.LookaheadCount >= 2 && user.X == reference.Look1X && user.Y == reference.Look1Y
                && firstTo.X == reference.Look2X && firstTo.Y == reference.Look2Y)
                relation = 1;  // AHEAD: the edge after the reference's next
            else
            {
                gateNotes.Add($"{reference.VirtualId}:no-relation(first=({user.X},{user.Y})->({firstTo.X},{firstTo.Y}) refLook={reference.LookaheadCount})");
                rejectReason ??= "no-route-relation";
                continue;
            }

            Console.WriteLine($"[ROOMAUTH_FORMATION_CANDIDATE] user={user.VirtualId} ref={reference.VirtualId} relation={relation} joinerFirst=({user.X},{user.Y})->({firstTo.X},{firstTo.Y}) refCurrent=({reference.X},{reference.Y})->({reference.SetX},{reference.SetY}) refLookahead={reference.LookaheadCount} delayMs={delay}");

            user.FormationRefVirtualId = reference.VirtualId;
            user.FormationRefGeneration = reference.WalkGeneration;
            user.FormationRelation = relation;
            user.FormationEntryTick = reference.NextBeatTick;
            user.SelfPaced = true;
            user.WalkGeneration++;
            Console.WriteLine($"[ROOMAUTH_FORMATION_RESERVE] user={user.VirtualId} ref={reference.VirtualId} relation={relation} entryTick={reference.NextBeatTick} leadTimeMs={delay} gen={user.WalkGeneration}");
            _ = SelfPaceWalk(user, (int)delay, true, user.WalkGeneration, reference.NextBeatTick);
            return true;
        }

        Console.WriteLine($"[ROOMAUTH_FORMATION_REJECT] user={user.VirtualId} kind=fresh reason={(humansNearby > 0 ? (rejectReason ?? "no-eligible-reference") : "no-human-nearby")} humansNearby={humansNearby} details={(gateNotes.Count > 0 ? string.Join("|", gateNotes) : "-")}");

        return false;
    }

    // pixelrp formation admission for MID-WALK REDIRECTS - the dominant real
    // follow pattern (players spam-click while moving). The joiner's current
    // 500ms edge is untouched; its NEXT edge is re-anchored to the reference's
    // grid when the first reference boundary after the joiner's own boundary
    // is at most the redirect window away (default 60ms - rendered as a
    // sub-perceptual boundary clamp, far below every fault threshold). Each
    // redirect click is an independent admission draw, so a sustained follow
    // locks into exact whole-tile formation within a few clicks and then
    // stays locked (the shared anchor persists across later redirects).
    // Kill/tune: server_settings `pathfinder.formation.redirect.window.ms`.
    private bool TryReserveRedirectFormation(RoomUser user)
    {
        // See fresh-path note: missing settings keys read as "0", so 0 cannot
        // mean "off". Missing/0 -> default; explicit negative disables.
        var windowMs = 60;
        if (int.TryParse(PlusEnvironment.SettingsManager.TryGetValue("pathfinder.formation.redirect.window.ms"), out var configured) && configured != 0)
            windowMs = configured;
        if (windowMs < 0)
        {
            Console.WriteLine($"[ROOMAUTH_FORMATION_REJECT] user={user.VirtualId} kind=redirect reason=window-disabled");
            return false;
        }
        if (!user.SelfPaced || !user.SetStep)
        {
            Console.WriteLine($"[ROOMAUTH_FORMATION_REJECT] user={user.VirtualId} kind=redirect reason=joiner-not-selfpaced(sp={user.SelfPaced},ss={user.SetStep})");
            return false;
        }

        var now = Environment.TickCount64;
        var ownBoundary = user.NextBeatTick;
        if (ownBoundary <= now)
        {
            Console.WriteLine($"[ROOMAUTH_FORMATION_REJECT] user={user.VirtualId} kind=redirect reason=own-beat-stale({ownBoundary - now}ms)");
            return false;
        }

        var rejectReason = (string)null;
        var humansNearby = 0;
        var gateNotes = new List<string>();

        foreach (var reference in GetUserList().ToList())
        {
            if (reference == null || reference == user || reference.IsBot || reference.IsPet) continue;
            if (Math.Max(Math.Abs(reference.X - user.X), Math.Abs(reference.Y - user.Y)) > 3) continue;
            humansNearby++;
            if (!reference.SelfPaced || !reference.IsWalking || !reference.SetStep)
            {
                gateNotes.Add($"{reference.VirtualId}:not-walking(sp={reference.SelfPaced},w={reference.IsWalking},ss={reference.SetStep})");
                rejectReason ??= "ref-not-walking";
                continue;
            }
            if (reference.NextBeatTick <= now)
            {
                gateNotes.Add($"{reference.VirtualId}:beat-stale");
                rejectReason ??= "ref-beat-stale";
                continue;
            }

            // First reference boundary at or after the joiner's own boundary.
            var gap = (int)(((reference.NextBeatTick - ownBoundary) % 500 + 500) % 500);
            if (gap > windowMs)
            {
                gateNotes.Add($"{reference.VirtualId}:window({gap}ms)");
                rejectReason ??= $"window-exceeded gapMs={gap}";
                continue;
            }
            var entryTick = ownBoundary + gap;

            // The joiner's entry edge starts from its CURRENT edge's goal.
            var preview = PathFinder.FindPath(user, _room.GetGameMap().DiagonalEnabled, _room.GetGameMap(),
                new(user.SetX, user.SetY), new(user.GoalX, user.GoalY));
            if (preview == null || preview.Count <= 1)
            {
                gateNotes.Add($"{reference.VirtualId}:no-route");
                rejectReason ??= "no-route";
                break;
            }
            var firstTo = preview[preview.Count - 2];

            var relation = 99;
            if (user.SetX == reference.X && user.SetY == reference.Y && firstTo.X == reference.SetX && firstTo.Y == reference.SetY)
                relation = -1;
            else if (reference.LookaheadCount >= 1 && user.SetX == reference.SetX && user.SetY == reference.SetY
                && firstTo.X == reference.Look1X && firstTo.Y == reference.Look1Y)
            {
                // Same-tile merge suppression - see the fresh-path note.
                if (PlusEnvironment.SettingsManager.TryGetValue("pathfinder.formation.allow.aligned") != "1")
                {
                    gateNotes.Add($"{reference.VirtualId}:aligned-suppressed");
                    rejectReason ??= "aligned-overlap-suppressed";
                    continue;
                }
                relation = 0;
            }
            else if (reference.LookaheadCount >= 2 && user.SetX == reference.Look1X && user.SetY == reference.Look1Y
                && firstTo.X == reference.Look2X && firstTo.Y == reference.Look2Y)
                relation = 1;
            else
            {
                gateNotes.Add($"{reference.VirtualId}:no-relation(entry=({user.SetX},{user.SetY})->({firstTo.X},{firstTo.Y}) refCur=({reference.X},{reference.Y})->({reference.SetX},{reference.SetY}) refLook={reference.LookaheadCount})");
                rejectReason ??= "no-route-relation";
                continue;
            }

            Console.WriteLine($"[ROOMAUTH_FORMATION_CANDIDATE] user={user.VirtualId} ref={reference.VirtualId} kind=redirect relation={relation} joinerEntry=({user.SetX},{user.SetY})->({firstTo.X},{firstTo.Y}) refCurrent=({reference.X},{reference.Y})->({reference.SetX},{reference.SetY}) gapMs={gap}");

            user.FormationRefVirtualId = reference.VirtualId;
            user.FormationRefGeneration = reference.WalkGeneration;
            user.FormationRelation = relation;
            user.FormationEntryTick = entryTick;
            user.WalkGeneration++;
            Console.WriteLine($"[ROOMAUTH_FORMATION_RESERVE] user={user.VirtualId} ref={reference.VirtualId} kind=redirect relation={relation} entryTick={entryTick} leadTimeMs={entryTick - now} gapMs={gap} gen={user.WalkGeneration}");
            _ = SelfPaceWalk(user, 1, false, user.WalkGeneration, entryTick);
            return true;
        }

        Console.WriteLine($"[ROOMAUTH_FORMATION_REJECT] user={user.VirtualId} kind=redirect reason={(humansNearby > 0 ? (rejectReason ?? "no-eligible-reference") : "no-human-nearby")} humansNearby={humansNearby} details={(gateNotes.Count > 0 ? string.Join("|", gateNotes) : "-")}");

        return false;
    }

    // pixelrp self-paced walk: after an instant first step, drive THIS unit's
    // remaining steps on an absolute-time metronome (no drift accumulation —
    // late steps stutter the client's fixed per-tile animation window) that
    // CONVERGES onto the shared wall-clock 500ms grid by lengthening beats
    // ≤30ms each, so all walkers end up phase-locked and formations render in
    // whole-tile offsets (see the formation-lock comment in the loop).
    // The global tick skips a SelfPaced unit's movement (see OnCycle), so the
    // two never double-step; both take _cycleLock. Ends when the unit stops,
    // arrives, or leaves — handing movement back to the global tick.
    // allowPreWalkFirstBeat: the first beat may fire for a unit that is not
    // walking yet but has a pending PathRecalcNeeded (the rate-capped click) —
    // that beat pathfinds and emits the first step itself.
    private async Task SelfPaceWalk(RoomUser user, int firstBeatDelayMs, bool allowPreWalkFirstBeat, long generation, long anchorTick = 0)
    {
        const int BeatMs = 500;
        try
        {
            var beat = 0;
            // Formation admission passes the reference's ABSOLUTE boundary
            // tick: re-deriving the anchor from "now + delay" here shifted the
            // joiner's entire grid by the reservation-to-task latency, leaving
            // a permanent few-ms offset from the reference. With the absolute
            // anchor, every member's beat chain is anchor + 500k with the SAME
            // anchor value - shared roomStepStart is exact (deltaMs = 0).
            var next = (anchorTick > 0) ? anchorTick : (Environment.TickCount64 + firstBeatDelayMs);
            // pixelrp movement authority: STRICT 500ms metronome. Every beat
            // fires exactly BeatMs after the previous one, anchored at the
            // instant first step - no grid alignment, no slew, no variable
            // timing of any kind. Consecutive edges are exactly contiguous on
            // the wire (cycleStart[n+1] = cycleStart[n] + 500), which is what
            // lets clients roll over with ZERO boundary waits. Cross-walker
            // phase alignment is deliberately absent (deferred to a future
            // formation pass); every scheduled step carries exact timing and
            // is timeline-renderable.
            while (true)
            {
                beat++;
                var onGrid = true;
                user.NextBeatTick = next;
                var waitMs = next - Environment.TickCount64;
                if (waitMs > 0) await Task.Delay((int)waitMs);
                lock (_cycleLock)
                {
                    // pixelrp lag-hunt instrumentation: a beat firing late is
                    // an authoritative-packet gap the client cannot hide.
                    var lateMs = Environment.TickCount64 - next;
                    if (lateMs > 100)
                        Console.WriteLine($"[ROOMAUTH_SRV] user={user.VirtualId} event=beat-late lateMs={lateMs} beat={beat} scheduled={next} onGrid={onGrid} seq={user.MovementSeq} gen={generation}");
                    // Superseded by a newer loop? Exit without touching state —
                    // SelfPaced now belongs to the newer generation.
                    if (user.WalkGeneration != generation) return;
                    if (!IsValid(user) || !user.SelfPaced)
                    {
                        user.SelfPaced = false;
                        return;
                    }
                    var preWalkStart = allowPreWalkFirstBeat && beat == 1
                        && !user.IsWalking && user.PathRecalcNeeded;
                    // pixelrp formation admission: the reserved entry beat has
                    // arrived. Validate lightly and log - the walk proceeds
                    // identically either way (the anchor is already this beat's
                    // exact schedule; relation and spacing are emergent route
                    // truth, so no correction ever exists to apply or skip).
                    if (beat == 1 && user.FormationRefVirtualId > 0)
                    {
                        var formationRef = GetRoomUserByVirtualId(user.FormationRefVirtualId);
                        var refValid = (formationRef != null && formationRef.SelfPaced && formationRef.IsWalking
                            && formationRef.WalkGeneration == user.FormationRefGeneration);
                        Console.WriteLine(refValid
                            ? $"[ROOMAUTH_FORMATION_ENTER] user={user.VirtualId} ref={user.FormationRefVirtualId} relation={user.FormationRelation} entryTick={user.FormationEntryTick} beatTick={next}"
                            : $"[ROOMAUTH_FORMATION_CANCEL] user={user.VirtualId} ref={user.FormationRefVirtualId} relation={user.FormationRelation} reason={(formationRef == null ? "ref-gone" : (!formationRef.IsWalking ? "ref-stopped" : "ref-repathed"))}");
                        user.FormationRefVirtualId = 0;
                    }
                    if (!user.IsWalking && !preWalkStart)
                    {
                        user.SelfPaced = false;
                        return;
                    }
                    var removed = false;
                    user.NextStepScheduledTick = next;
                    user.NextStepOnGrid = onGrid;
                    ProcessUserMovement(user, new List<RoomUser>(), out removed);
                    if (preWalkStart) user.LastInstantStep = DateTime.Now;
                    SerializeStatusUpdates();
                    if (user.IsWalking && !user.SetStep)
                        Console.WriteLine($"[ROOMAUTH_SRV] user={user.VirtualId} event=beat-no-step beat={beat} seq={user.MovementSeq} gen={generation}");
                    if (removed || !user.IsWalking)
                    {
                        Console.WriteLine($"[ROOMAUTH_SRV] user={user.VirtualId} event=loop-exit reason={(removed ? "removed" : "arrived")} beat={beat} seq={user.MovementSeq} gen={generation} pathPending={user.PathRecalcNeeded}");
                        user.SelfPaced = false;
                        return;
                    }
                }
                next += BeatMs;
            }
        }
        catch (Exception e)
        {
            try { if (user.WalkGeneration == generation) user.SelfPaced = false; } catch { }
            ExceptionLogger.LogException(e);
        }
    }

    public void OnCycle()
    {
        var userCounter = 0;
        List<RoomUser> dirtyPositions = null;
        try
        {
            lock (_cycleLock)
            {
                var toRemove = new List<RoomUser>();
                foreach (var user in GetUserList().ToList())
                {
                    if (user == null)
                        continue;
                    if (!IsValid(user))
                    {
                        if (user.GetClient() != null)
                            RemoveUserFromRoom(user.GetClient(), false);
                        else
                            RemoveRoomUser(user);
                    }
                    if (user.NeedsAutokick && !toRemove.Contains(user))
                    {
                        toRemove.Add(user);
                        continue;
                    }
                    if (user.NeedsIdleDisconnect)
                    {
                        user.GetClient()?.Disconnect();
                        continue;
                    }
                    var updated = false;
                    user.IdleTime++;
                    user.HandleSpamTicks();
                    // pixelrp aggression decay: a full bar (100) drains over 45
                    // seconds (500ms tick -> 100/90 per tick); broadcast each
                    // tick while active so every HUD strip tracks it live.
                    // pixelrp passive status: count down ONLINE seconds; whisper on
                // every minute boundary, and once more when it expires. Saved on
                // each whisper so a crash/logout loses <60s of countdown.
                // pixelrp :walk - staff-forced patrol. Re-evaluated every
                // cycle so the user reverses at the lane end without pausing;
                // the same lane scan the bot patrol uses (GenericBot).
                if (!user.IsBot && user.ForcedWalkHorizontal is { } forcedHorizontal && !user.IsWalking && user.CanWalk)
                {
                    var lane = FindForcedWalkEnd(user.X, user.Y, forcedHorizontal, user.ForcedWalkDirection);
                    if (lane.X == user.X && lane.Y == user.Y)
                    {
                        user.ForcedWalkDirection = -user.ForcedWalkDirection;
                        lane = FindForcedWalkEnd(user.X, user.Y, forcedHorizontal, user.ForcedWalkDirection);
                    }
                    if (lane.X != user.X || lane.Y != user.Y)
                        user.MoveTo(lane.X, lane.Y);
                }
                if (!user.IsBot && user.GetClient()?.GetHabbo() is { RpPassiveSeconds: > 0 } habboPas)
                {
                    var nowTick = Environment.TickCount64;
                    if (habboPas.RpPassiveLastTick == 0)
                        habboPas.RpPassiveLastTick = nowTick;
                    // pixelrp safe zones: the countdown freezes here. Keep
                    // re-anchoring the decrement clock every tick so none of
                    // the time spent in this room counts once they step back
                    // into an unsafe room.
                    if (_room.IsSafeZone)
                    {
                        habboPas.RpPassiveLastTick = nowTick;
                    }
                    else
                    {
                        var elapsedSec = (int)((nowTick - habboPas.RpPassiveLastTick) / 1000);
                        if (elapsedSec >= 1)
                        {
                            habboPas.RpPassiveLastTick += elapsedSec * 1000L;
                            var beforeMinutes = (habboPas.RpPassiveSeconds + 59) / 60;
                            habboPas.RpPassiveSeconds = Math.Max(0, habboPas.RpPassiveSeconds - elapsedSec);
                            var afterMinutes = (habboPas.RpPassiveSeconds + 59) / 60;
                            if (habboPas.RpPassiveSeconds == 0)
                            {
                                user.GetClient().SendWhisper("Your passive status has expired.");
                                habboPas.SaveRpStats();
                                _room.SendPacket(new RpStatsComposer(user.VirtualId, habboPas.RpHealth, habboPas.RpHealthMax, habboPas.RpEnergy, habboPas.RpEnergyMax, (int)Math.Round(habboPas.RpAggression), 0, habboPas.Rank >= 5 ? 1 : 0));
                            }
                            else if (afterMinutes < beforeMinutes)
                            {
                                user.GetClient().SendWhisper($"Your passive status expires in {afterMinutes} minutes.");
                                habboPas.SaveRpStats();
                            }
                        }
                    }
                }
                if (!user.IsBot && user.GetClient()?.GetHabbo() is { RpAggression: > 0 } habboAgg)
                    {
                        habboAgg.RpAggression = Math.Max(0, habboAgg.RpAggression - (100.0 / 90.0));
                        _room.SendPacket(new RpStatsComposer(user.VirtualId, habboAgg.RpHealth, habboAgg.RpHealthMax, habboAgg.RpEnergy, habboAgg.RpEnergyMax, (int)Math.Round(habboAgg.RpAggression), habboAgg.RpPassiveSeconds > 0 ? 1 : 0, habboAgg.Rank >= 5 ? 1 : 0));
                    }
                    if (!user.IsBot && !user.IsAsleep && user.IdleTime >= 600)
                    {
                        user.IsAsleep = true;
                        _room.SendPacket(new SleepComposer(user, true));
                    }
                    if (user.CarryItemId > 0)
                    {
                        user.CarryTimer--;
                        if (user.CarryTimer <= 0)
                            user.CarryItem(0);
                    }
                    if (_room.GotFreeze())
                        _room.GetFreeze().CycleUser(user);
                    var removed = false;
                    if (!user.SelfPaced)
                    {
                        updated = ProcessUserMovement(user, toRemove, out removed);
                        if (removed) continue;
                    }
                    if (user.RidingHorse)
                        user.ApplyEffect(77);
                    if (user.IsBot && user.BotAi != null)
                        user.BotAi.OnTimerTick();
                    else
                        userCounter++;
                    if (!updated) UpdateUserEffect(user, user.X, user.Y);
                    if (IsPositionSaveDue(user))
                        (dirtyPositions ??= new List<RoomUser>()).Add(user);
                }
                if (dirtyPositions != null)
                    FlushPositions(dirtyPositions);
                foreach (var userToRemove in toRemove.ToList())
                {
                    var client = PlusEnvironment.Game.ClientManager.GetClientByUserId(userToRemove.HabboId);
                    if (client != null)
                        RemoveUserFromRoom(client, true);
                    else
                        RemoveRoomUser(userToRemove);
                }
                if (UserCount != userCounter)
                    UpdateUserCount(userCounter);
            }
        }
        catch (Exception e)
        {
            ExceptionLogger.LogCriticalException(e);
        }
    }

    /// <summary>
    /// True when this user's tile/rotation has changed since it was last written to `users`
    /// and the throttle has elapsed. Checked on the 500ms tick, so a walking player costs at
    /// most one write every <see cref="PositionSaveIntervalCycles"/> ticks and a standing one
    /// costs nothing.
    /// </summary>
    private static bool IsPositionSaveDue(RoomUser user)
    {
        if (user == null || user.IsBot || user.GetClient()?.GetHabbo() == null)
            return false;
        if (--user.PositionSaveCountdown > 0)
            return false;
        user.PositionSaveCountdown = PositionSaveIntervalCycles;
        return user.X != user.SavedX || user.Y != user.SavedY || user.RotBody != user.SavedRot;
    }

    /// <summary>
    /// Persists the given users' positions. This is what makes the restore survive terminations
    /// that never reach RemoveUserFromRoom - a crash, an OOM kill, a pulled network cable, or a
    /// disconnect while the user is between rooms.
    /// </summary>
    private void FlushPositions(List<RoomUser> users)
    {
        try
        {
            using var dbClient = PlusEnvironment.DatabaseManager.Connection();
            foreach (var user in users)
            {
                var habbo = user.GetClient()?.GetHabbo();
                if (habbo == null)
                    continue;
                dbClient.Execute(
                    "UPDATE `users` SET `last_room_id` = @roomId, `last_x` = @x, `last_y` = @y, `last_rot` = @rot, `home_room` = @roomId WHERE `id` = @userId LIMIT 1",
                    new
                    {
                        userId = habbo.Id,
                        roomId = _room.RoomId,
                        x = user.X,
                        y = user.Y,
                        rot = user.RotBody
                    });
                user.SavedX = user.X;
                user.SavedY = user.Y;
                user.SavedRot = user.RotBody;
                // Same mirroring the exit save does, so the logout write of HomeRoom cannot
                // put back a stale room id.
                habbo.HomeRoom = _room.RoomId;
            }
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    // pixelrp instant-first-step: extracted movement region (unchanged logic) so it can
    // be invoked both from the 500ms tick (OnCycle) and from the instant-first-step path.
    private bool ProcessUserMovement(RoomUser user, List<RoomUser> toRemove, out bool removed)
    {
        removed = false;
        var updated = false;
        var invalidStep = false;
        // pixelrp movement authority: consume the scheduled-step presets
        // EXACTLY ONCE per call. A beat that emits no step (arrival, blocked
        // tile) must never leak its schedule into a later walk's first step -
        // that stamped instant steps with cycleStarts up to seconds old
        // (proven by ROOMAUTH_SRV vs client capture: stale seq-46 at 9.7s).
        var presetStepTick = user.NextStepScheduledTick;
        var presetStepOnGrid = user.NextStepOnGrid;
        user.NextStepScheduledTick = 0;
        user.NextStepOnGrid = false;
        if (user.IsRolling)
                {
                    if (user.RollerDelay <= 0)
                    {
                        UpdateUserStatus(user, false);
                        user.IsRolling = false;
                    }
                    else
                        user.RollerDelay--;
                }
                if (user.SetStep)
                {
                    if (_room.GetGameMap().IsValidStep2(user, new(user.X, user.Y), new(user.SetX, user.SetY), user.GoalX == user.SetX && user.GoalY == user.SetY, user.AllowOverride))
                    {
                        if (!user.RidingHorse)
                            _room.GetGameMap().UpdateUserMovement(new(user.Coordinate.X, user.Coordinate.Y), new(user.SetX, user.SetY), user);
                        var coordinatedItems = _room.GetGameMap().GetCoordinatedItems(new(user.X, user.Y));
                        foreach (var item in coordinatedItems.ToList()) item.UserWalksOffFurni(user);
                        if (!user.IsBot)
                        {
                            user.X = user.SetX;
                            user.Y = user.SetY;
                            user.Z = user.SetZ;
                        }
                        else if (user.IsBot && !user.RidingHorse)
                        {
                            user.X = user.SetX;
                            user.Y = user.SetY;
                            user.Z = user.SetZ;
                        }
                        if (!user.IsBot && user.RidingHorse)
                        {
                            var horse = GetRoomUserByVirtualId(user.HorseId);
                            if (horse != null)
                            {
                                horse.X = user.SetX;
                                horse.Y = user.SetY;
                            }
                        }
                        // pixelrp: the door tile is not an exit — players leave rooms via
                        // teleports (staff also via navigator/hotel-view), never by walking out.
                        var items = _room.GetGameMap().GetCoordinatedItems(new(user.X, user.Y));
                        foreach (var item in items.ToList()) item.UserWalksOnFurni(user);
                        UpdateUserStatus(user, true);
                    }
                    else
                        invalidStep = true;
                    user.SetStep = false;
                }
                if (user.PathRecalcNeeded)
                {
                    if (user.Path.Count > 1)
                        user.Path.Clear();
                    user.Path = PathFinder.FindPath(user, _room.GetGameMap().DiagonalEnabled, _room.GetGameMap(), new(user.X, user.Y), new(user.GoalX, user.GoalY));
                    if (user.Path.Count > 1)
                    {
                        user.PathStep = 1;
                        user.IsWalking = true;
                        user.PathRecalcNeeded = false;
                    }
                    else
                    {
                        user.PathRecalcNeeded = false;
                        if (user.Path.Count > 1)
                            user.Path.Clear();
                    }
                }
                if (user.IsWalking && !user.Freezed)
                {
                    if (invalidStep || user.PathStep >= user.Path.Count || user.GoalX == user.X && user.GoalY == user.Y) //No path found, or reached goal (:
                    {
                        user.IsWalking = false;
                        user.RemoveStatus("mv");
                        if (user.Statusses.ContainsKey("sign"))
                            user.RemoveStatus("sign");
                        if (user.IsBot && user.BotData.TargetUser > 0)
                        {
                            if (user.CarryItemId > 0)
                            {
                                var target = _room.GetRoomUserManager().GetRoomUserByHabbo(user.BotData.TargetUser);
                                if (target != null && Gamemap.TilesTouching(user.X, user.Y, target.X, target.Y))
                                {
                                    user.SetRot(Rotation.Calculate(user.X, user.Y, target.X, target.Y), false);
                                    target.SetRot(Rotation.Calculate(target.X, target.Y, user.X, user.Y), false);
                                    target.CarryItem(user.CarryItemId);
                                }
                            }
                            user.CarryItem(0);
                            user.BotData.TargetUser = 0;
                        }
                        if (user.RidingHorse && user.IsPet == false && !user.IsBot)
                        {
                            var mascotaVinculada = GetRoomUserByVirtualId(user.HorseId);
                            if (mascotaVinculada != null)
                            {
                                mascotaVinculada.IsWalking = false;
                                mascotaVinculada.RemoveStatus("mv");
                                mascotaVinculada.UpdateNeeded = true;
                            }
                        }
                    }
                    else
                    {
                        var nextStep = user.Path[user.Path.Count - user.PathStep - 1];
                        user.PathStep++;
                        if (user.FastWalking && user.PathStep < user.Path.Count)
                        {
                            var s2 = user.Path.Count - user.PathStep - 1;
                            nextStep = user.Path[s2];
                            user.PathStep++;
                        }
                        if (user.SuperFastWalking && user.PathStep < user.Path.Count)
                        {
                            var s2 = user.Path.Count - user.PathStep - 1;
                            nextStep = user.Path[s2];
                            user.PathStep++;
                            user.PathStep++;
                        }
                        var nextX = nextStep.X;
                        var nextY = nextStep.Y;
                        user.RemoveStatus("mv");
                        if (_room.GetGameMap().IsValidStep2(user, new(user.X, user.Y), new(nextX, nextY), user.GoalX == nextX && user.GoalY == nextY, user.AllowOverride))
                        {
                            var nextZ = _room.GetGameMap().SqAbsoluteHeight(nextX, nextY);
                            if (!user.IsBot)
                            {
                                if (user.IsSitting)
                                {
                                    user.Statusses.Remove("sit");
                                    user.Z += 0.35;
                                    user.IsSitting = false;
                                    user.UpdateNeeded = true;
                                }
                                else if (user.IsLying)
                                {
                                    user.Statusses.Remove("sit");
                                    user.Z += 0.35;
                                    user.IsLying = false;
                                    user.UpdateNeeded = true;
                                }
                            }
                            if (!user.IsBot)
                            {
                                user.Statusses.Remove("lay");
                                user.Statusses.Remove("sit");
                            }
                            if (!user.IsBot && !user.IsPet && user.GetClient() != null)
                            {
                                if (user.GetClient().GetHabbo().IsTeleporting)
                                {
                                    user.GetClient().GetHabbo().IsTeleporting = false;
                                    user.GetClient().GetHabbo().TeleporterId = 0;
                                }
                                else if (user.GetClient().GetHabbo().IsHopping)
                                {
                                    user.GetClient().GetHabbo().IsHopping = false;
                                    user.GetClient().GetHabbo().HopperId = 0;
                                }
                            }
                            if (!user.IsBot && user.RidingHorse && user.IsPet == false)
                            {
                                var horse = GetRoomUserByVirtualId(user.HorseId);
                                if (horse != null)
                                    horse.SetStatus("mv", $"{nextX},{nextY},{TextHandling.GetString(nextZ)}");
                                user.SetStatus("mv", $"{+nextX},{nextY},{TextHandling.GetString(nextZ + 1)}");
                                user.UpdateNeeded = true;
                                horse.UpdateNeeded = true;
                            }
                            else
                                user.SetStatus("mv", $"{nextX},{nextY},{TextHandling.GetString(nextZ)}");
                            var newRot = Rotation.Calculate(user.X, user.Y, nextX, nextY, user.MoonwalkEnabled);
                            user.RotBody = newRot;
                            user.RotHead = newRot;
                            user.SetStep = true;
                            user.SetX = nextX;
                            user.SetY = nextY;
                            user.SetZ = nextZ;
                            // pixelrp movement authority: stamp this step's
                            // timing for RpMovementCycleComposer (sent by
                            // SerializeStatusUpdates). Scheduled beat time
                            // when the self-pace loop preset it, else "now".
                            user.MovementSeq++;
                            user.StepStartedTick = (presetStepTick > 0) ? presetStepTick : Environment.TickCount64;
                            user.StepOnGrid = presetStepOnGrid;
                            // Peek the walker's own REAL path for up to two
                            // future tiles (PathStep already advanced past the
                            // step just emitted; the list is goal-first).
                            user.LookaheadCount = 0;
                            if (!user.FastWalking && !user.SuperFastWalking)
                            {
                                if (user.PathStep < user.Path.Count && (user.GoalX != user.SetX || user.GoalY != user.SetY))
                                {
                                    var peek1 = user.Path[user.Path.Count - user.PathStep - 1];
                                    user.Look1X = peek1.X;
                                    user.Look1Y = peek1.Y;
                                    user.Look1Z100 = (int)Math.Round(_room.GetGameMap().SqAbsoluteHeight(peek1.X, peek1.Y) * 100);
                                    user.LookaheadCount = 1;
                                    if ((user.PathStep + 1) < user.Path.Count && (user.GoalX != peek1.X || user.GoalY != peek1.Y))
                                    {
                                        var peek2 = user.Path[user.Path.Count - user.PathStep - 2];
                                        user.Look2X = peek2.X;
                                        user.Look2Y = peek2.Y;
                                        user.Look2Z100 = (int)Math.Round(_room.GetGameMap().SqAbsoluteHeight(peek2.X, peek2.Y) * 100);
                                        user.LookaheadCount = 2;
                                    }
                                }
                            }
                            UpdateUserEffect(user, user.SetX, user.SetY);
                            updated = true;
                            if (user.RidingHorse && user.IsPet == false && !user.IsBot)
                            {
                                var horse = GetRoomUserByVirtualId(user.HorseId);
                                if (horse != null)
                                {
                                    horse.RotBody = newRot;
                                    horse.RotHead = newRot;
                                    horse.SetStep = true;
                                    horse.SetX = nextX;
                                    horse.SetY = nextY;
                                    horse.SetZ = nextZ;
                                }
                            }
                            // Only mutate the pathfinding map for user occupancy when room
                            // blocking is on. With pixelrp's global tile-overlap
                            // (RoomBlockingEnabled always true) we leave the map untouched so
                            // it reflects only walls/furni - the per-step SqState
                            // backup/restore dance could otherwise corrupt an occupied tile
                            // to "blocked" over time, which is what made standing on the same
                            // tile stop working after a while.
                            if (!_room.RoomBlockingEnabled)
                            {
                                _room.GetGameMap().GameMap[user.X, user.Y] = user.SqState; // REstore the old one
                                user.SqState = _room.GetGameMap().GameMap[user.SetX, user.SetY]; //Backup the new one
                                var users = _room.GetRoomUserManager().GetUserForSquare(nextX, nextY);
                                if (users != null)
                                    _room.GetGameMap().GameMap[nextX, nextY] = 0;
                            }
                        }
                    }
                    if (!user.RidingHorse)
                        user.UpdateNeeded = true;
                }
                else
                {
                    if (user.Statusses.ContainsKey("mv"))
                    {
                        user.RemoveStatus("mv");
                        user.UpdateNeeded = true;
                        if (user.RidingHorse)
                        {
                            var horse = GetRoomUserByVirtualId(user.HorseId);
                            if (horse != null)
                            {
                                horse.RemoveStatus("mv");
                                horse.UpdateNeeded = true;
                            }
                        }
                    }
                }
        return updated;
    }

    public void UpdateUserStatus(RoomUser user, bool cyclegameitems)
    {
        if (user == null)
            return;
        try
        {
            var isBot = user.IsBot;
            if (isBot)
                cyclegameitems = false;
            if (UnixTimestamp.GetNow() > UnixTimestamp.GetNow() + user.SignTime)
            {
                if (user.Statusses.ContainsKey("sign"))
                {
                    user.Statusses.Remove("sign");
                    user.UpdateNeeded = true;
                }
            }
            if (user.Statusses.ContainsKey("lay") && !user.IsLying || user.Statusses.ContainsKey("sit") && !user.IsSitting)
            {
                if (user.Statusses.ContainsKey("lay"))
                    user.Statusses.Remove("lay");
                if (user.Statusses.ContainsKey("sit"))
                    user.Statusses.Remove("sit");
                user.UpdateNeeded = true;
            }
            else if (user.IsLying || user.IsSitting)
                return;
            double newZ;
            var itemsOnSquare = _room.GetGameMap().GetAllRoomItemForSquare(user.X, user.Y);
            if (itemsOnSquare != null || itemsOnSquare.Count != 0)
            {
                if (user.RidingHorse && user.IsPet == false)
                    newZ = _room.GetGameMap().SqAbsoluteHeight(user.X, user.Y, itemsOnSquare.ToList()) + 1;
                else
                    newZ = _room.GetGameMap().SqAbsoluteHeight(user.X, user.Y, itemsOnSquare.ToList());
            }
            else
                newZ = 1;
            if (newZ != user.Z)
            {
                user.Z = newZ;
                user.UpdateNeeded = true;
            }
            var model = _room.GetGameMap().Model;
            if (model.SqState[user.X, user.Y] == SquareState.Seat)
            {
                if (!user.Statusses.ContainsKey("sit"))
                    user.Statusses.Add("sit", "1.0");
                user.Z = model.SqFloorHeight[user.X, user.Y];
                user.RotHead = model.SqSeatRot[user.X, user.Y];
                user.RotBody = model.SqSeatRot[user.X, user.Y];
                user.UpdateNeeded = true;
            }
            if (itemsOnSquare.Count == 0)
                user.LastItem = null;
            foreach (var item in itemsOnSquare.ToList())
            {
                if (item == null)
                    continue;
                if (item.Definition.IsSeat)
                {
                    if (!user.Statusses.ContainsKey("sit"))
                    {
                        if (!user.Statusses.ContainsKey("sit"))
                            user.Statusses.Add("sit", TextHandling.GetString(item.Definition.Height));
                    }
                    user.Z = item.GetZ;
                    user.RotHead = item.Rotation;
                    user.RotBody = item.Rotation;
                    user.UpdateNeeded = true;
                }
                switch (item.Definition.InteractionType)
                {
                    case InteractionType.Bed:
                    case InteractionType.TentSmall:
                        {
                            if (!user.Statusses.ContainsKey("lay"))
                                user.Statusses.Add("lay", $"{TextHandling.GetString(item.Definition.Height)} null");
                            user.Z = item.GetZ;
                            user.RotHead = item.Rotation;
                            user.RotBody = item.Rotation;
                            user.UpdateNeeded = true;
                            break;
                        }
                    case InteractionType.Banzaigategreen:
                    case InteractionType.Banzaigateblue:
                    case InteractionType.Banzaigatered:
                    case InteractionType.Banzaigateyellow:
                        {
                            if (cyclegameitems)
                            {
                                var effectId = Convert.ToInt32(item.Team + 32);
                                var t = user.GetClient().GetHabbo().CurrentRoom.GetTeamManagerForBanzai();
                                if (user.Team == Team.None)
                                {
                                    if (t.CanEnterOnTeam(item.Team))
                                    {
                                        if (user.Team != Team.None)
                                            t.OnUserLeave(user);
                                        user.Team = item.Team;
                                        t.AddUser(user);
                                        if (user.GetClient().GetHabbo().Effects.CurrentEffect != effectId)
                                            user.GetClient().GetHabbo().Effects.ApplyEffect(effectId);
                                    }
                                }
                                else if (user.Team != Team.None && user.Team != item.Team)
                                {
                                    t.OnUserLeave(user);
                                    user.Team = Team.None;
                                    user.GetClient().GetHabbo().Effects.ApplyEffect(0);
                                }
                                else
                                {
                                    //usersOnTeam--;
                                    t.OnUserLeave(user);
                                    if (user.GetClient().GetHabbo().Effects.CurrentEffect == effectId)
                                        user.GetClient().GetHabbo().Effects.ApplyEffect(0);
                                    user.Team = Team.None;
                                }
                                //Item.ExtraData = usersOnTeam.ToString();
                                //Item.UpdateState(false, true);
                            }
                            break;
                        }
                    case InteractionType.FreezeYellowGate:
                    case InteractionType.FreezeRedGate:
                    case InteractionType.FreezeGreenGate:
                    case InteractionType.FreezeBlueGate:
                        {
                            if (cyclegameitems)
                            {
                                var effectId = Convert.ToInt32(item.Team + 39);
                                var t = user.GetClient().GetHabbo().CurrentRoom.GetTeamManagerForFreeze();
                                if (user.Team == Team.None)
                                {
                                    if (t.CanEnterOnTeam(item.Team))
                                    {
                                        if (user.Team != Team.None)
                                            t.OnUserLeave(user);
                                        user.Team = item.Team;
                                        t.AddUser(user);
                                        if (user.GetClient().GetHabbo().Effects.CurrentEffect != effectId)
                                            user.GetClient().GetHabbo().Effects.ApplyEffect(effectId);
                                    }
                                }
                                else if (user.Team != Team.None && user.Team != item.Team)
                                {
                                    t.OnUserLeave(user);
                                    user.Team = Team.None;
                                    user.GetClient().GetHabbo().Effects.ApplyEffect(0);
                                }
                                else
                                {
                                    //usersOnTeam--;
                                    t.OnUserLeave(user);
                                    if (user.GetClient().GetHabbo().Effects.CurrentEffect == effectId)
                                        user.GetClient().GetHabbo().Effects.ApplyEffect(0);
                                    user.Team = Team.None;
                                }
                                //Item.ExtraData = usersOnTeam.ToString();
                                //Item.UpdateState(false, true);
                            }
                            break;
                        }
                    case InteractionType.Banzaitele:
                        {
                            if (user.Statusses.ContainsKey("mv"))
                                _room.GetGameItemHandler().OnTeleportRoomUserEnter(user, item);
                            break;
                        }
                    case InteractionType.Effect:
                        {
                            if (user == null)
                                return;
                            if (!user.IsBot)
                            {
                                if (item == null || item.Definition == null || user.GetClient() == null || user.GetClient().GetHabbo() == null || user.GetClient().GetHabbo().Effects == null)
                                    return;
                                if (item.Definition.EffectId == 0 && user.GetClient().GetHabbo().Effects.CurrentEffect == 0)
                                    return;
                                user.GetClient().GetHabbo().Effects.ApplyEffect(item.Definition.EffectId);
                                item.LegacyDataString = "1";
                                item.UpdateState(false, true);
                                item.RequestUpdate(2, true);
                            }
                            break;
                        }
                    case InteractionType.Arrow:
                        {
                            if (user.GoalX == item.GetX && user.GoalY == item.GetY)
                            {
                                if (user == null || user.GetClient() == null || user.GetClient().GetHabbo() == null)
                                    continue;
                                var room = user.GetClient().GetHabbo().CurrentRoom;
                                if (room == null)
                                    return;
                                if (!ItemTeleporterFinder.IsTeleLinked(item.Id, room))
                                    user.UnlockWalking();
                                else
                                {
                                    var linkedTele = ItemTeleporterFinder.GetLinkedTele(item.Id);
                                    var teleRoomId = ItemTeleporterFinder.GetTeleRoomId(linkedTele, room);
                                    if (teleRoomId == room.RoomId)
                                    {
                                        var targetItem = room.GetRoomItemHandler().GetItem(linkedTele);
                                        if (targetItem == null)
                                        {
                                            if (user.GetClient() != null)
                                                user.GetClient().SendWhisper("Hey, that arrow is poorly!");
                                            return;
                                        }
                                        room.GetGameMap().TeleportToItem(user, targetItem);
                                    }
                                    else if (teleRoomId != room.RoomId)
                                    {
                                        if (user != null && !user.IsBot && user.GetClient() != null && user.GetClient().GetHabbo() != null)
                                        {
                                            user.GetClient().GetHabbo().IsTeleporting = true;
                                            user.GetClient().GetHabbo().TeleportingRoomId = teleRoomId;
                                            user.GetClient().GetHabbo().TeleporterId = linkedTele;
                                            user.GetClient().GetHabbo().PrepareRoom(teleRoomId, "");
                                        }
                                    }
                                    else if (_room.GetRoomItemHandler().GetItem(linkedTele) != null)
                                    {
                                        user.SetPos(item.GetX, item.GetY, item.GetZ);
                                        user.SetRot(item.Rotation, false);
                                    }
                                    else
                                        user.UnlockWalking();
                                }
                            }
                            break;
                        }
                }
            }
            if (user.IsSitting && user.TeleportEnabled)
            {
                user.Z -= 0.35;
                user.UpdateNeeded = true;
            }
            if (cyclegameitems)
            {
                if (_room.GotSoccer())
                    _room.GetSoccer().OnUserWalk(user);
                if (_room.GotBanzai())
                    _room.GetBanzai().OnUserWalk(user);
                if (_room.GotFreeze())
                    _room.GetFreeze().OnUserWalk(user);
            }
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
    }

    private void UpdateUserEffect(RoomUser user, int x, int y)
    {
        if (user == null || user.IsBot || user.GetClient() == null || user.GetClient().GetHabbo() == null)
            return;
        try
        {
            var newCurrentUserItemEffect = _room.GetGameMap().EffectMap[x, y];
            if (newCurrentUserItemEffect > 0)
            {
                if (user.GetClient().GetHabbo().Effects.CurrentEffect == 0)
                    user.CurrentItemEffect = ItemEffectType.None;
                var type = ByteToItemEffectEnum.Parse(newCurrentUserItemEffect);
                if (type != user.CurrentItemEffect)
                {
                    switch (type)
                    {
                        case ItemEffectType.Iceskates:
                            {
                                user.GetClient().GetHabbo().Effects.ApplyEffect(user.GetClient().GetHabbo().Gender == "M" ? 38 : 39);
                                user.CurrentItemEffect = ItemEffectType.Iceskates;
                                break;
                            }
                        case ItemEffectType.Normalskates:
                            {
                                user.GetClient().GetHabbo().Effects.ApplyEffect(user.GetClient().GetHabbo().Gender == "M" ? 55 : 56);
                                user.CurrentItemEffect = type;
                                break;
                            }
                        case ItemEffectType.Swim:
                            {
                                user.GetClient().GetHabbo().Effects.ApplyEffect(29);
                                user.CurrentItemEffect = type;
                                break;
                            }
                        case ItemEffectType.SwimLow:
                            {
                                user.GetClient().GetHabbo().Effects.ApplyEffect(30);
                                user.CurrentItemEffect = type;
                                break;
                            }
                        case ItemEffectType.SwimHalloween:
                            {
                                user.GetClient().GetHabbo().Effects.ApplyEffect(37);
                                user.CurrentItemEffect = type;
                                break;
                            }
                        case ItemEffectType.None:
                            {
                                user.GetClient().GetHabbo().Effects.ApplyEffect(-1);
                                user.CurrentItemEffect = type;
                                break;
                            }
                    }
                }
            }
            else if (user.CurrentItemEffect != ItemEffectType.None && newCurrentUserItemEffect == 0)
            {
                user.GetClient().GetHabbo().Effects.ApplyEffect(-1);
                user.CurrentItemEffect = ItemEffectType.None;
            }
        }
        catch { }
    }

    public ICollection<RoomUser> GetUserList() => _users.Values;

    public void Dispose()
    {
        UpdatePets();
        UpdateBots();
        _room.UsersNow = 0;
        using (var dbClient = PlusEnvironment.DatabaseManager.GetQueryReactor())
        {
            dbClient.RunQuery($"UPDATE `rooms` SET `users_now` = '0' WHERE `id` = '{_room.Id}' LIMIT 1");
        }
        _users.Clear();
        _pets.Clear();
        _bots.Clear();
        UserCount = 0;
        PetCount = 0;
        _users = null;
        _pets = null;
        _bots = null;
        _room = null;
    }

    // pixelrp :walk - furthest open tile along one axis from (x, y).
    private Point FindForcedWalkEnd(int x, int y, bool horizontal, int direction)
    {
        var map = _room.GetGameMap();
        var end = new Point(x, y);
        while (true)
        {
            var next = horizontal ? new Point(end.X + direction, end.Y) : new Point(end.X, end.Y + direction);
            if (!map.ValidTile(next.X, next.Y) || !map.SquareIsOpen(next.X, next.Y, false))
                return end;
            end = next;
        }
    }
}
