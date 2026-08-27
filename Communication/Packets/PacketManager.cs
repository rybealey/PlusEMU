using Microsoft.Extensions.Logging;
using Plus.Communication.Attributes;
using Plus.Communication.Packets.Incoming;
using Plus.HabboHotel.GameClients;
using System.Diagnostics;
using System.Reflection;

namespace Plus.Communication.Packets;

public sealed class PacketManager : IPacketManager, IDisposable
{
    private readonly ILogger<PacketManager> _logger;

    private readonly Dictionary<uint, IPacketEvent> _incomingPackets = new();
    private readonly HashSet<Type> _handshakePackets = new();
    private readonly HashSet<Type> _staffOnlyPackets = new();
    private readonly HashSet<Type> _vipOnlyPackets = new();
    private readonly Dictionary<uint, string> _packetNames = new();

    /// <summary>
    ///     The maximum time a task can run for before it is considered dead
    ///     (can be used for debugging any locking issues with certain areas of code)
    /// </summary>
    private readonly TimeSpan _maximumRunTimeInSec; // 5 minutes in debug. 30 seconds in release.
    private readonly CancellationTokenSource _cancellationTokenSource = new();

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
            if (packet.GetType().GetCustomAttribute<VipOnlyAttribute>() != null)
                _vipOnlyPackets.Add(packet.GetType());
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

        if (!_handshakePackets.Contains(pak.GetType()) && session.GetHabbo() == null)
        {
            _logger.LogDebug($"Session {session.Id} tried execute packet {messageId} but didn't handshake yet.");
            return;
        }

        // Staff-only surfaces (catalog, navigator, ...) are hidden client-side, so any
        // non-staff session sending one forged the packet (G-Earth or similar). Drop it
        // here so no handler ever runs on an unauthorized session.
        if (_staffOnlyPackets.Contains(pak.GetType()) && !session.GetHabbo().IsStaff)
        {
            _logger.LogWarning("Blocked staff-only packet {packet} from {username} (id {userId}, rank {rank}) — likely packet injection.",
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
        await task.WaitAsync(_maximumRunTimeInSec, _cancellationTokenSource.Token).ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                foreach (var e in t.Exception.Flatten().InnerExceptions)
                {
                    _logger.LogError("Error handling packet {packetId} for session {session} @ Habbo  {username}: {message} {stacktrace}", pak.GetType().Name, session.Id, session.GetHabbo()?.Username ?? string.Empty, e.Message, e.StackTrace);
                    session.Disconnect();
                }
            }
        });
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _incomingPackets.Clear();
        _handshakePackets.Clear();
        _packetNames.Clear();
    }
}