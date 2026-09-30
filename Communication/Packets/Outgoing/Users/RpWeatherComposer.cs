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
    private readonly int _overrideCode;
    private readonly int _pinnedMinutes;

    public uint MessageId => ServerPacketHeader.RpWeatherComposer;

    /// <summary>
    /// pixelrp City Panel: `overrideCode` (-1 for none) is sent AS the current
    /// code, so every reader shows the held weather; then, appended after the
    /// daily list so an older client reads the record it expects, whether it
    /// is held and the pinned time of day (-1 for none).
    /// </summary>
    public RpWeatherComposer(WeatherStation.Snapshot snapshot, int failures, int overrideCode = -1, int pinnedMinutes = -1)
    {
        _s = snapshot;
        _failures = failures;
        _overrideCode = overrideCode;
        _pinnedMinutes = pinnedMinutes;
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
        packet.WriteInteger(_overrideCode >= 0 ? _overrideCode : _s.Code);
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
        packet.WriteBoolean(_overrideCode >= 0);
        packet.WriteInteger(_pinnedMinutes);
    }
}
