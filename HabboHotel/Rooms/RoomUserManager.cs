using System.Drawing;
using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.Communication.Packets.Outgoing.Handshake;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Permissions;
using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.Core;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.Trading;
using Plus.HabboHotel.Users;
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
        // pixelrp Movement V2: bots and pets are enrolled here, not in
        // AddAvatarToRoom (which only ever sees human sessions). Without this
        // they would have no movement engine at all now that V1 is gone.
        Movement.MovementV2Bridge.OnUserEnter(_room, user);
        return user;
    }

    public void RemoveBot(int virtualId, bool kicked)
    {
        var user = GetRoomUserByVirtualId(virtualId);
        // Dequeue before teardown so nothing is staged for a unit that is gone.
        if (user != null)
            Movement.MovementV2Bridge.OnUserLeave(_room, user);
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
        _room.SendPacket(new RpStatsComposer(user.VirtualId, session.GetHabbo().RpHealth, session.GetHabbo().RpHealthMax, session.GetHabbo().RpEnergy, session.GetHabbo().RpEnergyMax, (int)Math.Round(session.GetHabbo().RpAggression), session.GetHabbo().IsRpPassive ? 1 : 0, session.GetHabbo().Rank >= 5 ? 1 : 0));
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
        // pixelrp: passive players wear the passive enable on entry so there is
        // no pop on room change; the per-tick helper is the safety net.
        // pixelrp: City Government on duty wears the staff enable instead.
        if (ShiftManager.IsStaffOnDuty(session.GetHabbo().Id) && session.GetHabbo().Effects != null)
            session.GetHabbo().Effects.ApplyEffect(Habbo.StaffDutyEffectId);
        else if (session.GetHabbo().RpPassiveSeconds > 0 && session.GetHabbo().Effects != null)
            session.GetHabbo().Effects.ApplyEffect(Habbo.PassiveEnableEffectId);
        // pixelrp: the one-shot RP-stats sends above (this user to the room, and
        // SendObjects everyone to this user) can land before the React HUD has
        // mounted its RpStatsEvent listener, so passive/aggression tags (and
        // non-default HP/energy) silently vanish on a fresh login. Re-deliver
        // this client's full room-stats view for the next few cycles, by which
        // point the HUD is listening. See RpStatsResyncTicks in the room cycle.
        user.RpStatsResyncTicks = 6;
        // pixelrp Movement V2: enrol this user with the movement scheduler.
        // Bots and pets stay on V1, so this only enrols human users.
        Movement.MovementV2Bridge.OnUserEnter(_room, user);
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
        // pixelrp Movement V2: dequeue BEFORE the rest of teardown, so nothing
        // can be staged or emitted for a unit that is already gone.
        Movement.MovementV2Bridge.OnUserLeave(_room, user);
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

    /// <summary>
    /// Broadcast every user whose status changed since the last pass.
    ///
    /// TAKES _cycleLock ITSELF, and must. It reads user.Statusses - a plain
    /// Dictionary - and clears UpdateNeeded, and since the V2 cutover the Q1
    /// outbound worker writes both under _cycleLock from a different thread on
    /// every movement frame. Room.ProcessRoom calls this from the room tick with
    /// no lock of its own, so before this the tick could enumerate Statusses
    /// while Q1 inserted "mv" into it. The lock is reentrant, so the callers
    /// that already hold it (ApplyMovementFrame, ApplyRpKnockout, OnCycle) are
    /// unaffected.
    /// </summary>
    public void SerializeStatusUpdates()
    {
        lock (_cycleLock)
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

                // The V1 follow-up here sent RpMovementCycleComposer (3955) for
                // freshly stepped walkers. It is GONE: 3955 is retired, the
                // client no longer registers it, and the timing it carried is
                // now in the 4110 record ApplyMovementFrame sends immediately
                // after this broadcast. Its gate (user.SetStep) had no writer
                // left either, so it was emitting nothing.
            }
        }
    }

    /// <summary>
    /// pixelrp Movement V2: apply one sealed movement frame and emit it.
    /// Called by the Q1 outbound worker, NEVER by the movement scheduler.
    ///
    /// Runs under _cycleLock so RoomUser and Statusses have exactly one writer.
    ///
    /// WIRE ORDER IS PART OF THE CONTRACT: the UserUpdate carrying "mv" for an
    /// edge must reach the client BEFORE that edge's 4110 record. "mv" drives
    /// the walking posture and facing (native Nitro reads it for the animation);
    /// 4110 carries only the authoritative timing the renderer interpolates on.
    /// Sending 4110 first would briefly describe timing for a posture the client
    /// has not been told about yet.
    ///
    /// This is no longer the V1 bridge: 4110 is what actually renders movement,
    /// and RoomUser is updated because it is SERVER TRUTH that the rest of the
    /// hotel reads (chat range, furni, wired, occupancy) - not because V1 needs it.
    /// </summary>
    public void ApplyMovementFrame(Movement.MovementEdgeRecord[] frame, long serverNowMs)
    {
        if (frame == null || frame.Length == 0)
            return;
        lock (_cycleLock)
        {
            foreach (var edge in frame)
            {
                var user = GetRoomUserByVirtualId(edge.VirtualId);
                if (user == null || !IsValid(user))
                    continue;

                // 1. Server truth: the avatar has ARRIVED on this edge's from-tile
                //    (the previous edge's terminal).
                if (user.X != edge.FromX || user.Y != edge.FromY)
                {
                    var previous = new Point(user.X, user.Y);
                    var arrived = new Point(edge.FromX, edge.FromY);
                    _room.GetGameMap().UpdateUserMovement(previous, arrived, user);
                    foreach (var item in _room.GetGameMap().GetCoordinatedItems(previous).ToList())
                        item.UserWalksOffFurni(user);

                    user.X = edge.FromX;
                    user.Y = edge.FromY;
                    user.Z = edge.FromZ100 / 100.0;

                    foreach (var item in _room.GetGameMap().GetCoordinatedItems(arrived).ToList())
                        item.UserWalksOnFurni(user);

                    UpdateUserStatus(user, true);
                }

                // 2. Posture/facing for the edge now in flight, or the stop.
                if (edge.IsWalkEnd || edge.IsDisplacement)
                {
                    user.IsWalking = false;
                    user.RemoveStatus("mv");
                }
                else
                {
                    user.RotBody = edge.Facing;
                    user.RotHead = edge.Facing;
                    user.IsWalking = true;
                    user.SetStatus("mv",
                        $"{edge.ToX},{edge.ToY},{TextHandling.GetString(edge.ToZ)}");
                }
                user.UpdateNeeded = true;
            }

            // UserUpdate ("mv") FIRST ...
            SerializeStatusUpdates();

            // ... then the authoritative timing for each edge.
            foreach (var edge in frame)
                _room.SendPacket(new RpMovementV2Composer(edge, serverNowMs));
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
                                // pixelrp: drop the passive enable if it is the shown effect.
                                if (habboPas.Effects != null && habboPas.Effects.CurrentEffect == Habbo.PassiveEnableEffectId)
                                    habboPas.Effects.ApplyEffect(0);
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
                        _room.SendPacket(new RpStatsComposer(user.VirtualId, habboAgg.RpHealth, habboAgg.RpHealthMax, habboAgg.RpEnergy, habboAgg.RpEnergyMax, (int)Math.Round(habboAgg.RpAggression), habboAgg.IsRpPassive ? 1 : 0, habboAgg.Rank >= 5 ? 1 : 0));
                    }
                    if (!user.IsBot && !user.IsAsleep && user.IdleTime >= 600)
                    {
                        user.IsAsleep = true;
                        _room.SendPacket(new SleepComposer(user, true));
                        // pixelrp: going idle ends any active shift (progress banked)
                        if (!user.IsBot && user.GetClient() != null)
                            Corporations.ShiftManager.InterruptForIdle(user.GetClient());
                    }
                    if (user.CarryItemId > 0)
                    {
                        user.CarryTimer--;
                        if (user.CarryTimer <= 0)
                            user.CarryItem(0);
                    }
                    // pixelrp: restore an enable paused by the "67" gesture once
                    // the ~1s client animation has finished.
                    if (user.EffectReapplyTimer > 0)
                    {
                        user.EffectReapplyTimer--;
                        if (user.EffectReapplyTimer <= 0 && !user.IsDancing)
                        {
                            var effect = user.GetClient()?.GetHabbo()?.Effects?.CurrentEffect ?? 0;
                            if (effect > 0)
                                _room.SendPacket(new AvatarEffectComposer(user.VirtualId, effect));
                        }
                    }
                    if (_room.GotFreeze())
                        _room.GetFreeze().CycleUser(user);
                    // pixelrp Movement V2 owns every walker; the tick no longer
                    // moves anyone. What survives is the roller settle-down,
                    // which is not movement - it just counts a rolled user back
                    // to a normal posture.
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
                    if (user.RidingHorse)
                        user.ApplyEffect(77);
                    if (user.IsBot && user.BotAi != null)
                        user.BotAi.OnTimerTick();
                    else
                        userCounter++;
                    UpdateUserEffect(user, user.X, user.Y);
                    UpdatePassiveEffect(user);
                    if (user.RpStatsResyncTicks > 0)
                    {
                        user.RpStatsResyncTicks--;
                        ResyncRoomStatsTo(user);
                    }
                    if (IsPositionSaveDue(user))
                        (dirtyPositions ??= new List<RoomUser>()).Add(user);
                }
                if (dirtyPositions != null)
                {
                    // Snapshot in-memory here; the MySQL writes run on a
                    // background task, OFF _cycleLock and off the tick
                    // thread. This flush used to execute synchronous UPDATEs
                    // while holding the lock every walker's 500ms beat needs
                    // to emit its step - one slow query stalled EVERY
                    // walker's beat by the same amount (field: beat-late
                    // bursts of 124-991ms hitting all users at one tick).
                    var positionSnapshots = new List<object>();
                    foreach (var user in dirtyPositions)
                    {
                        var habbo = user.GetClient()?.GetHabbo();
                        if (habbo == null)
                            continue;
                        positionSnapshots.Add(new
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
                    if (positionSnapshots.Count > 0)
                        _ = Task.Run(() => FlushPositions(positionSnapshots));
                }
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
    private static void FlushPositions(List<object> positionSnapshots)
    {
        try
        {
            using var dbClient = PlusEnvironment.DatabaseManager.Connection();
            foreach (var snapshot in positionSnapshots)
            {
                dbClient.Execute(
                    "UPDATE `users` SET `last_room_id` = @roomId, `last_x` = @x, `last_y` = @y, `last_rot` = @rot, `home_room` = @roomId WHERE `id` = @userId LIMIT 1",
                    snapshot);
            }
        }
        catch (Exception e)
        {
            ExceptionLogger.LogException(e);
        }
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

    // pixelrp: the passive enable (Squad.nitro, id 248) is a resuming BASELINE
    // effect worn while the player is passive. It is asserted only when the
    // effect slot is free (0 or -1) and the player is not mid-dance, so a
    // dance/ride/tile/item effect temporarily owns the slot and the enable
    // comes back on the next tick once that transient effect clears. When the
    // player is no longer passive, the enable (and only the enable) is cleared.
    private void UpdatePassiveEffect(RoomUser user)
    {
        if (user == null || user.IsBot)
            return;
        var habbo = user.GetClient()?.GetHabbo();
        if (habbo?.Effects == null)
            return;

        var cur = habbo.Effects.CurrentEffect;
        // pixelrp: on duty for City Government the staff enable owns the slot
        // (same dance/lay exceptions as the passive enable below).
        if (ShiftManager.IsStaffOnDuty(habbo.Id))
        {
            if (cur != Habbo.StaffDutyEffectId && (cur == 0 || cur == -1 || cur == Habbo.PassiveEnableEffectId) && !user.IsDancing && !user.IsLying)
                habbo.Effects.ApplyEffect(Habbo.StaffDutyEffectId);
        }
        else if (habbo.RpPassiveSeconds > 0)
        {
            // Not while dancing (ApplyEffect stops the dance) and not while
            // lying (LayCommand clears the effect to hold the lay pose; the
            // enable resumes once the player stands).
            if ((cur == 0 || cur == -1) && !user.IsDancing && !user.IsLying)
                habbo.Effects.ApplyEffect(Habbo.PassiveEnableEffectId);
        }
        else if (cur == Habbo.PassiveEnableEffectId)
        {
            habbo.Effects.ApplyEffect(0);
        }
    }

    // pixelrp: re-deliver every room member's RP stats to one freshly-entered
    // viewer. The HUD stores stats by roomIndex from RpStatsEvent; the one-shot
    // entry sends can arrive before its listener mounts, so we repeat them for a
    // few cycles (RpStatsResyncTicks) until the tags reliably land.
    private void ResyncRoomStatsTo(RoomUser viewer)
    {
        var session = viewer?.GetClient();
        if (session == null)
            return;
        foreach (var other in _users.Values)
        {
            if (other == null || other.IsBot || other.IsPet)
                continue;
            var otherHabbo = other.GetClient()?.GetHabbo();
            if (otherHabbo == null)
                continue;
            session.Send(new RpStatsComposer(other.VirtualId, otherHabbo.RpHealth, otherHabbo.RpHealthMax, otherHabbo.RpEnergy, otherHabbo.RpEnergyMax, (int)Math.Round(otherHabbo.RpAggression), otherHabbo.IsRpPassive ? 1 : 0, otherHabbo.Rank >= 5 ? 1 : 0));
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
