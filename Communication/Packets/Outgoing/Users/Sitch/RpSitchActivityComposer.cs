using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Sitch;

namespace Plus.Communication.Packets.Outgoing.Users.Sitch;

/// <summary>pixelrp: what happened to the viewer on Sitch while they were away.</summary>
public class RpSitchActivityComposer : IServerPacket
{
    private readonly List<SitchUtility.ActivityRow> _rows;

    public uint MessageId => ServerPacketHeader.RpSitchActivityComposer;

    public RpSitchActivityComposer(List<SitchUtility.ActivityRow> rows) => _rows = rows;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_rows.Count);
        foreach (var r in _rows)
        {
            packet.WriteInteger(r.Id);
            packet.WriteInteger(r.ActorId);
            packet.WriteString(r.ActorName ?? "");
            packet.WriteString(r.ActorFigure ?? "");
            packet.WriteString(r.Kind ?? "");
            packet.WriteInteger(r.PostId);
            packet.WriteString(r.PostBody ?? "");
            packet.WriteInteger(r.CreatedAt);
            packet.WriteInteger(r.Seen);
        }
    }
}
