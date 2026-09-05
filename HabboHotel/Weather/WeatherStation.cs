using System.Globalization;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing.Users;

namespace Plus.HabboHotel.Weather;

/// <summary>
/// pixelrp: the phone's Weather app. One background loop pulls the real San
/// Francisco's weather from Open-Meteo (free, no key) every ten minutes and
/// pushes the whole snapshot to everyone online, so the hotel shares one sky
/// and the hotel makes one outbound call, not one per phone. Labels are
/// formatted here in Pacific time so the client only paints.
/// </summary>
public static class WeatherStation
{
    private const string Url =
        "https://api.open-meteo.com/v1/forecast?latitude=37.7749&longitude=-122.4194" +
        "&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,weather_code,wind_speed_10m,wind_direction_10m,wind_gusts_10m" +
        "&hourly=temperature_2m,weather_code,precipitation_probability,is_day,visibility,dew_point_2m,uv_index" +
        "&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset" +
        "&temperature_unit=fahrenheit&wind_speed_unit=mph&precipitation_unit=inch&timezone=America%2FLos_Angeles&forecast_days=10";

    private const int RefreshSeconds = 600;
    private const int RetrySeconds = 60;
    private const int HourlyCount = 24;

    public class Hour { public string Label = ""; public int Temp; public int Code; public int Precip; public int IsDay; }
    public class Day { public string Label = ""; public int Code; public int Lo; public int Hi; }

    public class Snapshot
    {
        public int FetchedAt;
        public string LocalTime = "";
        public int Temp, FeelsLike, Humidity, Code, IsDay, Wind, Gusts, WindDir, VisibilityTenths, DewPoint, UvTenths, Hi, Lo;
        public string Sunrise = "", Sunset = "";
        public List<Hour> Hourly = new();
        public List<Day> Daily = new();
    }

    private static readonly NLog.ILogger Log = NLog.LogManager.GetLogger("Plus.HabboHotel.Weather");
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly object Lock = new();
    private static Snapshot _snapshot;
    private static int _failures;
    // last: the loop starts once the type is first touched (first login or first phone open)
    private static readonly Task Loop = Task.Run(RunLoop);

    /// <summary>Forces the type initializer, i.e. starts the fetch loop.</summary>
    public static void Touch() { _ = Loop; }

    public static RpWeatherComposer Compose()
    {
        lock (Lock) return new RpWeatherComposer(_snapshot, _failures);
    }

    private static async Task RunLoop()
    {
        while (true)
        {
            var ok = await Refresh();
            try { await Task.Delay(TimeSpan.FromSeconds(ok ? RefreshSeconds : RetrySeconds)); }
            catch (Exception) { return; }
        }
    }

    private static async Task<bool> Refresh()
    {
        try
        {
            var json = await Http.GetStringAsync(Url);
            var snapshot = Parse(json);
            lock (Lock) { _snapshot = snapshot; _failures = 0; }
            Broadcast();
            return true;
        }
        catch (Exception e)
        {
            var first = false;
            lock (Lock) { _failures++; first = _failures == 1; }
            Log.Warn($"[weather] refresh failed ({_failures}): {e.Message}");
            // tell the phones once that we're on the last good reading
            if (first) Broadcast();
            return false;
        }
    }

    private static void Broadcast()
    {
        try { PlusEnvironment.Game?.ClientManager?.SendPacket(Compose()); }
        catch (Exception e) { Log.Warn($"[weather] broadcast failed: {e.Message}"); }
    }

    // ---- parsing ----------------------------------------------------------

    private static int I(JsonElement e) => e.ValueKind == JsonValueKind.Number ? (int)Math.Round(e.GetDouble()) : 0;
    private static double D(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : 0;
    private static string S(JsonElement e) => e.ValueKind == JsonValueKind.String ? (e.GetString() ?? "") : "";
    private static JsonElement At(JsonElement arr, int i) => (arr.ValueKind == JsonValueKind.Array && i < arr.GetArrayLength()) ? arr[i] : default;

    private static string HourLabel(string iso)
    {
        // "2026-09-06T15:00" -> "3PM"
        if (iso.Length < 13 || !int.TryParse(iso.AsSpan(11, 2), out var h)) return "";
        var twelve = ((h + 11) % 12) + 1;
        return $"{twelve}{(h >= 12 ? "PM" : "AM")}";
    }

    private static string ClockLabel(string iso)
    {
        // "2026-09-06T06:48" -> "6:48 AM"
        if (iso.Length < 16 || !int.TryParse(iso.AsSpan(11, 2), out var h)) return "";
        var twelve = ((h + 11) % 12) + 1;
        return $"{twelve}:{iso.Substring(14, 2)} {(h >= 12 ? "PM" : "AM")}";
    }

    private static string DayLabel(string iso, int index)
    {
        if (index == 0) return "Today";
        return DateTime.TryParseExact(iso.Length >= 10 ? iso.Substring(0, 10) : iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("ddd", CultureInfo.InvariantCulture) : "";
    }

    private static Snapshot Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var cur = root.GetProperty("current");
        var hourly = root.GetProperty("hourly");
        var daily = root.GetProperty("daily");

        var s = new Snapshot { FetchedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        var nowIso = S(cur.GetProperty("time"));
        s.LocalTime = nowIso.Length >= 16 ? nowIso.Substring(11, 5) : "";
        s.Temp = I(cur.GetProperty("temperature_2m"));
        s.FeelsLike = I(cur.GetProperty("apparent_temperature"));
        s.Humidity = I(cur.GetProperty("relative_humidity_2m"));
        s.Code = I(cur.GetProperty("weather_code"));
        s.IsDay = I(cur.GetProperty("is_day"));
        s.Wind = I(cur.GetProperty("wind_speed_10m"));
        s.Gusts = I(cur.GetProperty("wind_gusts_10m"));
        s.WindDir = I(cur.GetProperty("wind_direction_10m"));

        // hourly from the current hour on
        var times = hourly.GetProperty("time");
        var start = 0;
        var hourKey = nowIso.Length >= 13 ? nowIso.Substring(0, 13) : "";
        for (var i = 0; i < times.GetArrayLength(); i++)
        {
            if (S(times[i]).StartsWith(hourKey)) { start = i; break; }
        }
        var temps = hourly.GetProperty("temperature_2m");
        var codes = hourly.GetProperty("weather_code");
        var precip = hourly.GetProperty("precipitation_probability");
        var isDay = hourly.GetProperty("is_day");
        for (var i = start; i < Math.Min(times.GetArrayLength(), start + HourlyCount); i++)
        {
            s.Hourly.Add(new Hour { Label = i == start ? "Now" : HourLabel(S(times[i])), Temp = I(At(temps, i)), Code = I(At(codes, i)), Precip = I(At(precip, i)), IsDay = I(At(isDay, i)) });
        }
        s.VisibilityTenths = (int)Math.Round(D(At(hourly.GetProperty("visibility"), start)) / 1609.34 * 10);
        s.DewPoint = I(At(hourly.GetProperty("dew_point_2m"), start));
        s.UvTenths = (int)Math.Round(D(At(hourly.GetProperty("uv_index"), start)) * 10);

        var dTimes = daily.GetProperty("time");
        var dCodes = daily.GetProperty("weather_code");
        var dMax = daily.GetProperty("temperature_2m_max");
        var dMin = daily.GetProperty("temperature_2m_min");
        for (var i = 0; i < dTimes.GetArrayLength(); i++)
        {
            s.Daily.Add(new Day { Label = DayLabel(S(dTimes[i]), i), Code = I(At(dCodes, i)), Lo = I(At(dMin, i)), Hi = I(At(dMax, i)) });
        }
        if (s.Daily.Count > 0) { s.Hi = s.Daily[0].Hi; s.Lo = s.Daily[0].Lo; }
        s.Sunrise = ClockLabel(S(At(daily.GetProperty("sunrise"), 0)));
        s.Sunset = ClockLabel(S(At(daily.GetProperty("sunset"), 0)));
        return s;
    }
}
