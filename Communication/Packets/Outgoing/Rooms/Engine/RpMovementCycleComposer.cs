using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// pixelrp movement authority: the timing of one walk step, sent to the room
/// right after the UserUpdateComposer that carries its "mv" status. Gives
/// clients the authoritative cycle origin BEFORE (or as) the edge begins, so
/// synchronized walkers render from one shared clock with zero phase
/// acquisition. onGrid marks steps scheduled on the shared wall-clock 500ms
/// grid (all on-grid walkers world-wide share one cycle origin); the instant
/// first step and global-tick steps are off-grid and render natively.
/// </summary>
public class RpMovementCycleComposer : IServerPacket
{
    private readonly int _virtualId;
    private readonly int _seq;
    private readonly bool _onGrid;
    private readonly int _fromX;
    private readonly int _fromY;
    private readonly int _fromZ100;
    private readonly int _toX;
    private readonly int _toY;
    private readonly int _toZ100;
    private readonly long _cycleStart;
    private readonly int _lookCount;
    private readonly int _l1X; private readonly int _l1Y; private readonly int _l1Z;
    private readonly int _l2X; private readonly int _l2Y; private readonly int _l2Z;

    public uint MessageId => ServerPacketHeader.RpMovementCycleComposer;

    public RpMovementCycleComposer(RoomUser user)
    {
        _virtualId = user.VirtualId;
        _seq = (int)(user.MovementSeq & 0x7fffffff);
        _onGrid = user.StepOnGrid;
        _fromX = user.X;
        _fromY = user.Y;
        _fromZ100 = (int)System.Math.Round(user.Z * 100);
        _toX = user.SetX;
        _toY = user.SetY;
        _toZ100 = (int)System.Math.Round(user.SetZ * 100);
        _cycleStart = user.StepStartedTick;
        _lookCount = user.LookaheadCount;
        _l1X = user.Look1X; _l1Y = user.Look1Y; _l1Z = user.Look1Z100;
        _l2X = user.Look2X; _l2Y = user.Look2Y; _l2Z = user.Look2Z100;
    }

    public void Compose(IOutgoingPacket packet)
    {
        // serverNow is sampled at compose time so the client's clock-offset
        // estimate reflects real send latency; cycleStart travels as a small
        // delta against it (TickCount64 itself does not fit an int).
        var now = System.Environment.TickCount64;

        packet.WriteInteger(_virtualId);
        packet.WriteInteger(_seq);
        packet.WriteInteger(_onGrid ? 1 : 0);
        packet.WriteInteger(_fromX);
        packet.WriteInteger(_fromY);
        packet.WriteInteger(_fromZ100);
        packet.WriteInteger(_toX);
        packet.WriteInteger(_toY);
        packet.WriteInteger(_toZ100);
        packet.WriteInteger((int)(_cycleStart - now));
        packet.WriteInteger(500);
        packet.WriteInteger((int)(now >> 32));
        packet.WriteInteger((int)(now & 0xffffffff));

        // Provisional lookahead: the walker's next 0-2 REAL path tiles, so
        // clients can queue future edges and never boundary-wait. From-tile,
        // timing and sequence are all derivable (contiguous 500ms chain).
        packet.WriteInteger(_lookCount);
        if (_lookCount >= 1) { packet.WriteInteger(_l1X); packet.WriteInteger(_l1Y); packet.WriteInteger(_l1Z); }
        if (_lookCount >= 2) { packet.WriteInteger(_l2X); packet.WriteInteger(_l2Y); packet.WriteInteger(_l2Z); }
    }
}
