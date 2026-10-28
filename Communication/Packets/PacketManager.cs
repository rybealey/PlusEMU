using Microsoft.Extensions.Logging;
using Plus.Communication.Attributes;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Handshake;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Security;
using System.Diagnostics;
using System.Reflection;

namespace Plus.Communication.Packets;

public sealed class PacketManager : IPacketManager, IDisposable
{
    private readonly ILogger<PacketManager> _logger;

    private readonly Dictionary<uint, IPacketEvent> _incomingPackets = new();
    private readonly HashSet<Type> _handshakePackets = new();
    private readonly HashSet<Type> _staffOnlyPackets = new();
    private readonly HashSet<Type> _builderOnlyPackets = new();
    private readonly HashSet<Type> _vipOnlyPackets = new();
    private readonly Dictionary<Type, PacketLimit> _rateLimits = new();
    private readonly Dictionary<uint, string> _packetNames = new();

    /// <summary>
    ///     The maximum time a task can run for before it is considered dead
    ///     (can be used for debugging any locking issues with certain areas of code)
    /// </summary>
    private readonly TimeSpan _maximumRunTimeInSec; // 5 minutes in debug. 30 seconds in release.
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    /// pixelrp (Twist, 2026-10-28): a login waits its turn behind a whole
    /// hotel reconnecting at once (SsoTicketEvent lets a few run at a time),
    /// and once it starts its ticket is spent - cut off at the 5 seconds a
    /// click gets, the player had to reload the website to try again.
    /// </summary>
    private static readonly TimeSpan LoginRunTime = TimeSpan.FromSeconds(30);

    public PacketManager(IEnumerable<IPacketEvent> incomingPackets, ILogger<PacketManager> logger)
    {
        _maximumRunTimeInSec = Debugger.IsAttached ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(5);
        _logger = logger;
        foreach (var packet in incomingPackets)
        {
            var field = typeof(ClientPacketHeader).GetField(packet.GetType().Name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (field == null)
            {
                _logger.LogWarning("No incoming header defined for {packet}", packet.GetType().Name);
                continue;
            }
            var header = (uint) field.GetValue(null);
            _incomingPackets.Add(header, packet);
            _packetNames.Add(header, packet.GetType().Name);
            if (packet.GetType().GetCustomAttribute<NoAuthenticationRequiredAttribute>() != null)
                _handshakePackets.Add(packet.GetType());
            if (packet.GetType().GetCustomAttribute<StaffOnlyAttribute>() != null)
                _staffOnlyPackets.Add(packet.GetType());
            if (packet.GetType().GetCustomAttribute<BuilderOnlyAttribute>() != null)
                _builderOnlyPackets.Add(packet.GetType());
            if (packet.GetType().GetCustomAttribute<VipOnlyAttribute>() != null)
                _vipOnlyPackets.Add(packet.GetType());
            // pixelrp: the packet's limit (PacketLimits). Walking has none - it
            // is the movement system's, never limited or counted towards a flood.
            if (PacketLimits.For(packet.GetType()) is { } limit)
                _rateLimits[packet.GetType()] = limit;
        }
    }

    public async Task TryExecutePacket(GameClient session, uint messageId, IIncomingPacket packet)
    {
        if (!_incomingPackets.TryGetValue(messageId, out var pak))
        {
            if (Debugger.IsAttached)
                _logger.LogDebug("Unhandled Packet: " + messageId);
            return;
        }

        if (Debugger.IsAttached)
        {
            if (_packetNames.ContainsKey(messageId))
                _logger.LogDebug("Handled Packet: [" + messageId + "] " + _packetNames[messageId]);
            else
                _logger.LogDebug("Handled Packet: [" + messageId + "] UnnamedPacketEvent");
        }

        // pixelrp: a connection's packets come out of a budget per packet type
        // and a total (PacketLimits) before any handler runs - before log in too.
        // Over it, the packet is dropped; a connection that keeps going far over it
        // is flooding, which only a packet tool or a bot does, so it is written
        // down for housekeeping (PacketFloodLog) and disconnected.
        if (_rateLimits.TryGetValue(pak.GetType(), out var limit))
        {
            var budget = PacketBudget.For(session);
            if (!budget.TryTake(pak.GetType(), limit, out var flooded))
            {
                if (flooded)
                {
                    var (packetName, packetDrops, drops) = budget.Worst();
                    PacketFloodLog.Record(session, packetName, packetDrops, drops, PacketBudget.FloodWindowMs / 1000);
                    session.Disconnect();
                }
                return;
            }
        }

        if (!_handshakePackets.Contains(pak.GetType()) && session.GetHabbo() == null)
        {
            _logger.LogDebug($"Session {session.Id} tried execute packet {messageId} but didn't handshake yet.");
            return;
        }

        // Staff-only surfaces (marketplace, hotel view, ...) are hidden client-side, so any
        // non-staff session sending one forged the packet (G-Earth or similar). Drop it
        // here so no handler ever runs on an unauthorized session.
        if (_staffOnlyPackets.Contains(pak.GetType()) && !session.GetHabbo().IsStaff)
        {
            _logger.LogWarning("Blocked staff-only packet {packet} from {username} (id {userId}, rank {rank}) — likely packet injection.",
                pak.GetType().Name, session.GetHabbo().Username, session.GetHabbo().Id, session.GetHabbo().Rank);
            return;
        }

        // pixelrp: the Furni Shop and the navigator open from Builder (Habbo.IsBuilder),
        // below the staff line, and are hidden client-side for everyone under it - so,
        // as above, a lower session sending one forged the packet.
        if (_builderOnlyPackets.Contains(pak.GetType()) && !session.GetHabbo().IsBuilder)
        {
            _logger.LogWarning("Blocked builder-only packet {packet} from {username} (id {userId}, rank {rank}) — likely packet injection.",
                pak.GetType().Name, session.GetHabbo().Username, session.GetHabbo().Id, session.GetHabbo().Rank);
            return;
        }

        // pixelrp: VIP-only surfaces (camera) are hidden client-side for lapsed/non-VIP
        // sessions, so any non-staff, non-VIP session sending one forged the packet
        // (G-Earth or similar). Drop it here so no handler ever runs on an unauthorized
        // session.
        if (_vipOnlyPackets.Contains(pak.GetType()) && !(session.GetHabbo().IsStaff || session.GetHabbo().IsVip))
        {
            _logger.LogWarning("Blocked VIP-only packet {packet} from {username} (id {userId}, rank {rank}) — likely packet injection.",
                pak.GetType().Name, session.GetHabbo().Username, session.GetHabbo().Id, session.GetHabbo().Rank);
            return;
        }

        await ExecutePacketAsync(session, packet, pak);
    }

    private async Task ExecutePacketAsync(GameClient session, IIncomingPacket packet, IPacketEvent pak)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
            return;

        var task = pak.Parse(session, packet);
        // pixelrp (Twist, 2026-10-28): a handler that has already finished - as
        // every click, walk and chat handler does, synchronously - ends here.
        // The ContinueWith that checked it always went through the thread pool,
        // even for a finished task, and this player's next packet waited on that
        // hop: microseconds on a quiet server, milliseconds once the pool is
        // busy. A fault or the time limit is logged and disconnects as before,
        // and a cancel (shutting down) does nothing, as before.
        if (task.IsCompletedSuccessfully)
            return;
        var runTime = pak is SsoTicketEvent && LoginRunTime > _maximumRunTimeInSec ? LoginRunTime : _maximumRunTimeInSec;
        try
        {
            await task.WaitAsync(runTime, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            IEnumerable<Exception> errors = task.IsFaulted && task.Exception != null ? task.Exception.Flatten().InnerExceptions : new[] { error };
            foreach (var e in errors)
                _logger.LogError("Error handling packet {packetId} for session {session} @ Habbo  {username}: {message} {stacktrace}", pak.GetType().Name, session.Id, session.GetHabbo()?.Username ?? string.Empty, e.Message, e.StackTrace);
            session.Disconnect();
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _incomingPackets.Clear();
        _handshakePackets.Clear();
        _packetNames.Clear();
    }
}