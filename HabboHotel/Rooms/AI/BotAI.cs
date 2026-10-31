using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.AI;

public abstract class BotAi
{
    private Room _room;
    private uint _roomId;
    private RoomUser _roomUser;
    private int _roomUserId;
    public int BaseId;

    public void Init(int baseId, int roomUserId, uint roomId, RoomUser user, Room room)
    {
        BaseId = baseId;
        _roomUserId = roomUserId;
        _roomId = roomId;
        _roomUser = user;
        _room = room;
    }

    public Room GetRoom() => _room;

    public RoomUser GetRoomUser() => _roomUser;

    public RoomBot GetBotData()
    {
        var user = GetRoomUser();
        if (user == null)
            return null;
        return GetRoomUser().BotData;
    }

    /// <summary>
    /// pixelrp: the bot's own walk (Twist, 2026-10-31). A walk stands it up
    /// first (RoomUser.MoveTo), so a bot on a seat or a bed whose walk went
    /// nowhere - a seat walled in, like the Courthouse judge's - was left
    /// standing on it. It sits back down instead: the route is planned inside
    /// the request, so a bot not walking straight after has nowhere to go.
    /// </summary>
    protected void WalkTo(int x, int y)
    {
        var user = GetRoomUser();
        if (user == null)
            return;
        var onFurni = user.HasStatus("sit") || user.HasStatus("lay");
        user.MoveTo(x, y);
        if (onFurni && !Plus.HabboHotel.Rooms.Movement.MovementV2Bridge.IsWalkingOrWaiting(user))
            GetRoom()?.GetRoomUserManager()?.UpdateUserStatus(user, false);
    }

    public abstract void OnSelfEnterRoom();
    public abstract void OnSelfLeaveRoom(bool kicked);
    public abstract void OnUserEnterRoom(RoomUser user);
    public abstract void OnUserLeaveRoom(GameClient client);
    public abstract void OnUserSay(RoomUser user, string message);
    public abstract void OnUserShout(RoomUser user, string message);
    public abstract void OnTimerTick();
}