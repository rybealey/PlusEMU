using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>
/// pixelrp: the Stocks app's whole screen in one packet - every corporation's
/// current stock against its capacity, plus the series behind its chart.
///
/// The ticker IS the acronym; the app shows nothing the player has to look up.
/// Samples arrive oldest-first with their timestamps, already strided down to
/// a sane number of points for the window asked for (see StockLedger).
/// </summary>
public class RpStocksComposer : IServerPacket
{
    public record Sample(int SampledAt, int Value);

    public record CorpStock(
        int Id, string Acronym, string Name, string Description,
        int Stock, int Capacity, List<Sample> Samples);

    private readonly List<CorpStock> _corps;
    private readonly int _windowMinutes;

    public uint MessageId => ServerPacketHeader.RpStocksComposer;

    public RpStocksComposer(List<CorpStock> corps, int windowMinutes)
    {
        _corps = corps;
        _windowMinutes = windowMinutes;
    }

    public void Compose(IOutgoingPacket packet)
    {
        // Echoed back so a reply that lands after the player has changed range
        // can be recognised as stale rather than painted.
        packet.WriteInteger(_windowMinutes);
        packet.WriteInteger(_corps.Count);
        foreach (var corp in _corps)
        {
            packet.WriteInteger(corp.Id);
            packet.WriteString(corp.Acronym ?? "");
            packet.WriteString(corp.Name ?? "");
            packet.WriteString(corp.Description ?? "");
            packet.WriteInteger(corp.Stock);
            packet.WriteInteger(corp.Capacity);
            packet.WriteInteger(corp.Samples.Count);
            foreach (var sample in corp.Samples)
            {
                packet.WriteInteger(sample.SampledAt);
                packet.WriteInteger(sample.Value);
            }
        }
    }
}
