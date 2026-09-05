using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Weather;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>pixelrp: Weather app opened - hand over the current snapshot (and start the fetch loop if it isn't running yet).</summary>
internal class RpGetWeatherEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (session.GetHabbo() == null) return Task.CompletedTask;
        WeatherStation.Touch();
        session.Send(WeatherStation.Compose());
        return Task.CompletedTask;
    }
}
