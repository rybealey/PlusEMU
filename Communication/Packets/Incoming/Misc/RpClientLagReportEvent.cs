using System.Collections.Concurrent;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Misc;

/// <summary>
/// pixelrp: the client's lag recorder (RpLagRecorder.ts) - a short report of
/// the frames that took too long and what the client was doing in them, for
/// finding a stutter on somebody's own machine (Twist, 2026-10-26). Written
/// to the log as "[lag]" lines; nothing is kept or acted on.
///
/// Staff only (the client only records for them), at most one report every
/// 1.5 seconds per player and 4000 characters, so it cannot fill the log.
/// </summary>
internal class RpClientLagReportEvent : IPacketEvent
{
    private const long GapMs = 1500;
    private const int MaxLength = 4000;

    private static readonly ConcurrentDictionary<int, long> LastReport = new();

    private static NLog.ILogger Log => NLog.LogManager.GetLogger("Plus.LagReport");

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session?.GetHabbo();
        if (habbo == null || !habbo.IsStaff || packet.Buffer.Length < sizeof(short))
            return Task.CompletedTask;

        var now = Environment.TickCount64;
        if (LastReport.TryGetValue(habbo.Id, out var last) && now - last < GapMs)
            return Task.CompletedTask;
        LastReport[habbo.Id] = now;

        var text = packet.ReadString();
        if (text.Length > MaxLength)
            text = text[..MaxLength];
        Log.Info("[lag] {0} (id {1}) {2}", habbo.Username, habbo.Id, text);
        return Task.CompletedTask;
    }
}
