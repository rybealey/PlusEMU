using Microsoft.Extensions.Logging;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Connection;

public class OpenFlatConnectionEvent : IPacketEvent
{
    private readonly ILogger<OpenFlatConnectionEvent> _logger;

    public OpenFlatConnectionEvent(ILogger<OpenFlatConnectionEvent> logger)
    {
        _logger = logger;
    }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roomId = packet.ReadUInt();
        var password = packet.ReadString();
        var habbo = session.GetHabbo();

        // The navigator opens from Builder (Habbo.IsBuilder), so a client below that can
        // only ever be entering a room the server sent it to (login spawn, summon,
        // follow-friend, teleport) or the room it is already in. Any other target is an
        // injected packet - drop it.
        var sentHere = roomId == habbo.AuthorizedRoomEntryId ||
            roomId == habbo.CurrentRoom?.Id ||
            (habbo.IsTeleporting && roomId == habbo.TeleportingRoomId);
        if (!sentHere && !habbo.IsBuilder)
        {
            _logger.LogWarning("Blocked unauthorized room entry to {roomId} from {username} (id {userId}, rank {rank}) — likely packet injection.",
                roomId, habbo.Username, habbo.Id, habbo.Rank);
            return Task.CompletedTask;
        }

        // pixelrp: below the staff line a navigator trip asks TravelGuard, as
        // following a friend does - or it would be a way out of handcuffs. Staff
        // go anywhere, as they always have.
        if (!sentHere && !habbo.IsStaff)
        {
            var cannot = Plus.HabboHotel.Users.TravelGuard.Refusal(session);
            if (cannot != null)
            {
                session.SendWhisper(cannot);
                return Task.CompletedTask;
            }
        }

        habbo.PrepareRoom(roomId, password);
        return Task.CompletedTask;
    }
}
