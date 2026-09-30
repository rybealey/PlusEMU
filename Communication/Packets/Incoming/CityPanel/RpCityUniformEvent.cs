using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.Corporations;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>pixelrp City Panel: open one uniform - a kind ("rank" / "prisoner"), a rank id and a gender.</summary>
internal class RpCityUniformEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var kind = packet.ReadString() == UniformManager.KindPrisoner ? UniformManager.KindPrisoner : UniformManager.KindRank;
        var rankId = packet.ReadInt();
        var gender = packet.ReadString() == "F" ? "F" : "M";
        if (!CityPanelAccess.Can(session.GetHabbo(), CityPanelAccess.Capability.Uniforms))
            return Task.CompletedTask;
        session.Send(new RpCityUniformComposer(kind, rankId, gender, UniformManager.Get(kind, rankId, gender)));
        return Task.CompletedTask;
    }
}
