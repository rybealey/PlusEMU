using Dapper;
using NLog;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Users.Security;

/// <summary>
/// pixelrp: a connection disconnected for flooding the server with packets
/// (PacketBudget) - far more of one kind than the client ever sends, the mark
/// of a packet tool or a bot. Written down in rp_packet_flood_log (migration
/// 291) for the housekeeping player page, and to the log.
///
/// Before log in there is no player to name, so only the log line is written.
/// A failed write never keeps the connection open: the caller disconnects
/// whatever happens here.
/// </summary>
public static class PacketFloodLog
{
    private static readonly Logger Log = LogManager.GetLogger("Plus.HabboHotel.Users.Security.PacketFloodLog");

    public static void Record(GameClient session, string packet, int packetDrops, int drops, int windowSeconds)
    {
        var habbo = session?.GetHabbo();
        var detail = $"{drops} packets over the limit in {windowSeconds}s, {packetDrops} of them {packet}";
        Log.Warn($"[PacketFlood] {habbo?.Username ?? $"session {session?.Id}"} (#{habbo?.Id ?? 0}) disconnected: {detail}");

        if (habbo == null)
            return;

        try
        {
            using var connection = PlusEnvironment.DatabaseManager.Connection();
            connection.Execute(
                "INSERT INTO `rp_packet_flood_log` (`user_id`, `username`, `packet`, `dropped`, `window_seconds`, `created_at`) " +
                "VALUES (@userId, @username, @packet, @drops, @windowSeconds, UNIX_TIMESTAMP())",
                new { userId = habbo.Id, username = habbo.Username, packet, drops, windowSeconds });
        }
        catch (Exception e)
        {
            Log.Error(e, $"[PacketFlood] could not write the flood log for #{habbo.Id}");
        }
    }
}
