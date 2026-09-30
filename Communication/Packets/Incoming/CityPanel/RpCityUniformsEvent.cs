using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>pixelrp City Panel: the Uniforms tab's list of wearers.</summary>
internal class RpCityUniformsEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (!CityPanelAccess.Can(session.GetHabbo(), CityPanelAccess.Capability.Uniforms))
            return Task.CompletedTask;
        session.Send(new RpCityUniformsComposer(CityUniforms.Corporations(), CityUniforms.Prisoner()));
        return Task.CompletedTask;
    }
}
