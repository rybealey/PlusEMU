using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Weather;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the hotel's weather snapshot for the phone. hasData 0 means
/// nothing fetched yet (loading); failures > 0 with data means the reading
/// is the last good one (offline banner).
/// </summary>
public class RpWeatherComposer : IServerPacket
{
    private readonly WeatherStation.Snapshot _s;
    private readonly int _failures;

    public uint MessageId => ServerPacketHeader.RpWeatherComposer;

    public RpWeatherComposer(WeatherStation.Snapshot snapshot, int failures)
    {
        _s = snapshot;
        _failures = failures;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_failures);
        packet.WriteInteger(_s == null ? 0 : 1);
        if (_s == null) return;
        packet.WriteInteger(_s.FetchedAt);
        packet.WriteString(_s.LocalTime);
        packet.WriteInteger(_s.Temp);
        packet.WriteInteger(_s.FeelsLike);
        packet.WriteInteger(_s.Humidity);
        packet.WriteInteger(_s.Code);
        packet.WriteInteger(_s.IsDay);
        packet.WriteInteger(_s.Wind);
        packet.WriteInteger(_s.Gusts);
        packet.WriteInteger(_s.WindDir);
        packet.WriteInteger(_s.VisibilityTenths);
        packet.WriteInteger(_s.DewPoint);
        packet.WriteInteger(_s.UvTenths);
        packet.WriteInteger(_s.Hi);
        packet.WriteInteger(_s.Lo);
        packet.WriteString(_s.Sunrise);
        packet.WriteString(_s.Sunset);
        packet.WriteInteger(_s.Hourly.Count);
        foreach (var h in _s.Hourly)
        {
            packet.WriteString(h.Label);
            packet.WriteInteger(h.Temp);
            packet.WriteInteger(h.Code);
            packet.WriteInteger(h.Precip);
            packet.WriteInteger(h.IsDay);
        }
        packet.WriteInteger(_s.Daily.Count);
        foreach (var d in _s.Daily)
        {
            packet.WriteString(d.Label);
            packet.WriteInteger(d.Code);
            packet.WriteInteger(d.Lo);
            packet.WriteInteger(d.Hi);
        }
    }
}
