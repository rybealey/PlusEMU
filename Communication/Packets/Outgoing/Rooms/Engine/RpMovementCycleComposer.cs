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
    }
}
