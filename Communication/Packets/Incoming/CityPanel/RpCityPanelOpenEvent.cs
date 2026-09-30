using Plus.Communication.Packets.Outgoing.CityPanel;
using Plus.HabboHotel.CityPanel;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

namespace Plus.Communication.Packets.Incoming.CityPanel;

/// <summary>pixelrp City Panel: opened - answer with what this staff member may do in it.</summary>
internal class RpCityPanelOpenEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        if (!CityPanelAccess.CanOpen(habbo))
            return Task.CompletedTask;
        session.Send(new RpCityPanelComposer(CityPanelAccess.Capabilities(habbo), SpawnCommand.Spawnable));
        return Task.CompletedTask;
    }
}
