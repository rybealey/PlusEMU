using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator.Fun;

/// <summary>
/// pixelrp: bring somebody to where you are standing - the tile, not the room.
///
/// A summon that dropped people at the door was a summon you then had to wait
/// for. They land on the summoner's own tile now, which is what "come here"
/// means, and the client draws two avatars on one square perfectly happily
/// (tile overlap is on hotel-wide - RoomBlockingEnabled).
///
/// Two paths, because reloading a room somebody is already standing in is a
/// black screen and a fresh room load for no reason:
///
///   same room      warp in place, no reload, no room-entry packets at all
///   anywhere else  the usual forward, with the arrival tile carried over
///
/// The arrival tile rides on PendingRoomRestore, which already exists to put a
/// player back where they logged out - it is the general "enter the room HERE"
/// marker, single-use and consumed on entry. Reusing it means the cross-room
/// case needs no new machinery and cannot drift from the login path.
/// </summary>
internal class SummonCommand : ITargetChatCommand
{
    private readonly IGameClientManager _gameClientManager;
    public string Key => "summon";
    public string PermissionRequired => "command_summon";

    public string Parameters => "%username%";

    public string Description => "Bring another user to the tile you are standing on.";

    public bool MustBeInSameRoom => false;

    public SummonCommand(IGameClientManager gameClientManager)
    {
        _gameClientManager = gameClientManager;
    }

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var summoner = session.GetHabbo();
        if (summoner == null || target?.Client == null)
            return Task.CompletedTask;

        if (target.Username == summoner.Username)
        {
            session.SendWhisper("Get a life.");
            return Task.CompletedTask;
        }

        var current = summoner.CurrentRoom;
        if (current == null)
        {
            session.SendWhisper("You need to be in a room to summon somebody to it.");
            return Task.CompletedTask;
        }

        // The summoner's own avatar, which is the thing being summoned TO. No
        // avatar means they are between rooms; there is no tile to name yet.
        var summonerUser = current.GetRoomUserManager()?.GetRoomUserByHabbo(summoner.Id);
        if (summonerUser == null)
        {
            session.SendWhisper("You need to be in a room to summon somebody to it.");
            return Task.CompletedTask;
        }

        // Already here: move them, do not re-enter them. A PrepareRoom on the
        // room you are standing in tears down the room view and builds it
        // again, which reads as a glitch rather than a summon.
        if (target.CurrentRoom != null && target.CurrentRoom.RoomId == current.RoomId)
        {
            var targetUser = current.GetRoomUserManager()?.GetRoomUserByHabbo(target.Id);
            if (targetUser != null)
            {
                current.GetGameMap().TeleportToTile(targetUser, summonerUser.X, summonerUser.Y,
                    summonerUser.Z, summonerUser.RotBody);
                target.Client.SendNotification($"You have been summoned to {summoner.Username}!");
                return Task.CompletedTask;
            }
        }

        // Elsewhere, or nowhere. Set the arrival tile BEFORE the forward: the
        // marker is read when the room is entered, which begins the moment the
        // client acts on either call below.
        target.PendingRestore = new PendingRoomRestore(current.RoomId, summonerUser.X, summonerUser.Y,
            summonerUser.RotBody);
        target.Client.SendNotification($"You have been summoned to {summoner.Username}!");
        if (!target.InRoom)
            target.Client.SendRoomForward(current.Id);
        else
            target.PrepareRoom(current.Id, "");
        return Task.CompletedTask;
    }
}
