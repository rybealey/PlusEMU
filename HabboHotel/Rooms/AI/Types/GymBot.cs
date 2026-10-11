using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Gym;

namespace Plus.HabboHotel.Rooms.AI.Types;

/// <summary>
/// pixelrp: the Gym bot - sells gym memberships.
///
/// Click it and pick Buy membership: 30c from the purse for an hour of online
/// time on the machines, stacked on whatever is left. Like the bank teller it
/// owns no logic of its own - GymState decides and records everything; the
/// bot checks the player is at the desk and says how it went. Nothing to
/// remember between clicks, so no conversation is kept.
/// </summary>
internal class GymBot : BotAi
{
    /// <summary>How far a player may stand and still be served - the teller's two tiles.</summary>
    private const int ServiceRange = 2;

    /// <summary>The bot bubble the rest of the emulator uses.</summary>
    private const int BotBubble = 2;

    public GymBot(int virtualId)
    {
    }

    public override void OnSelfEnterRoom() { }

    public override void OnSelfLeaveRoom(bool kicked) { }

    public override void OnUserEnterRoom(RoomUser user) { }

    public override void OnUserLeaveRoom(GameClient client) { }

    public override void OnUserSay(RoomUser user, string message) { }

    public override void OnUserShout(RoomUser user, string message) { }

    public override void OnTimerTick() { }

    /// <summary>A Buy membership click, from RpGymBotActionEvent.</summary>
    public void Serve(GameClient session)
    {
        var habbo = session?.GetHabbo();
        var user = habbo == null ? null : GetRoom()?.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id);
        if (user == null || KnockedOut.Refuse(session!))
            return;

        var name = habbo!.Username;
        if (!InRange(user))
        {
            Say($"Come over to the desk and I'll sign you up, {name}.");
            return;
        }

        if (!GymState.BuyMembership(session!))
        {
            Say($"A membership is {GymState.MembershipCost}c, {name}. Come back when you have it.");
            return;
        }

        var minutes = (GymState.MembershipLeft(habbo) + 59) / 60;
        Say($"Thanks, {name}! You have {minutes} minutes of gym time.");
    }

    private bool InRange(RoomUser user)
    {
        var self = GetRoomUser();
        return self != null && Gamemap.TileDistance(self.X, self.Y, user.X, user.Y) <= ServiceRange;
    }

    private void Say(string message) => GetRoomUser()?.Chat(message, BotBubble);
}
