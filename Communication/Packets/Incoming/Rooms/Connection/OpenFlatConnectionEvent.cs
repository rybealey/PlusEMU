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

        // The navigator is staff-only, so a non-staff client can only ever be entering a
        // room the server sent it to (login spawn, summon, follow-friend, teleport) or the
        // room it is already in. Any other target is an injected packet - drop it.
        if (!habbo.IsStaff &&
            roomId != habbo.AuthorizedRoomEntryId &&
            roomId != habbo.CurrentRoom?.Id &&
            !(habbo.IsTeleporting && roomId == habbo.TeleportingRoomId))
        {
            _logger.LogWarning("Blocked unauthorized room entry to {roomId} from {username} (id {userId}, rank {rank}) — likely packet injection.",
                roomId, habbo.Username, habbo.Id, habbo.Rank);
            return Task.CompletedTask;
        }

        habbo.PrepareRoom(roomId, password);
        return Task.CompletedTask;
    }
}
